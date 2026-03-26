using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.DTOs;

namespace EnrichedMessaging.Application.Queries;

public sealed record GetDailyDigestQuery(Guid UserId) : IRequest<GetDailyDigestResult>;

public sealed record DigestMessageDto(
    Guid Id,
    AuthorDto Author,
    string Content,
    string MessageType,
    DateTime CreatedAt,
    QuoteDto? Quote,
    PollDataDto? Poll
);

public sealed record DigestGroupDto(Guid RoomId, string RoomName, IReadOnlyList<DigestMessageDto> Messages);

public sealed record GetDailyDigestResult(string Date, IReadOnlyList<DigestGroupDto> Groups);

public sealed class GetDailyDigestQueryHandler : IRequestHandler<GetDailyDigestQuery, GetDailyDigestResult>
{
    private readonly IDigestRepository _digest;
    private readonly IPollRepository _polls;

    public GetDailyDigestQueryHandler(IDigestRepository digest, IPollRepository polls)
    {
        _digest = digest;
        _polls = polls;
    }

    public async Task<GetDailyDigestResult> Handle(GetDailyDigestQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        var rawMessages = await _digest.GetMessagesAsync(request.UserId, yesterday, today, cancellationToken);

        var groups = rawMessages
            .GroupBy(m => (m.RoomId, m.RoomName))
            .OrderBy(g => g.Key.RoomName)
            .Select(async g =>
            {
                var msgs = new List<DigestMessageDto>();
                foreach (var m in g)
                {
                    PollDataDto? pollData = null;
                    if (m.MessageType == "poll")
                    {
                        var poll = await _polls.GetByMessageIdAsync(m.Id, request.UserId, cancellationToken);
                        if (poll is not null)
                        {
                            pollData = new PollDataDto(
                                poll.Id,
                                poll.Question,
                                poll.VoteMode,
                                poll.Options.Select(o => new PollOptionDto(o.Id, o.Text, o.DisplayOrder, o.VoteCount, o.VoterDisplayNames)).ToList(),
                                poll.CurrentUserVotedOptionIds
                            );
                        }
                    }

                    QuoteDto? quoteDto = null;
                    if (m.QuotedMessageId.HasValue)
                        quoteDto = new QuoteDto(m.QuotedMessageId, m.QuotedAuthorDisplayName ?? "Unknown", m.QuotedContentSnapshot ?? string.Empty, true);

                    msgs.Add(new DigestMessageDto(
                        m.Id,
                        new AuthorDto(m.UserId, m.DisplayName, m.AvatarUrl),
                        m.Content,
                        m.MessageType,
                        m.CreatedAt,
                        quoteDto,
                        pollData
                    ));
                }
                return new DigestGroupDto(g.Key.RoomId, g.Key.RoomName, msgs);
            });

        var resolvedGroups = await Task.WhenAll(groups);

        return new GetDailyDigestResult(yesterday.ToString("yyyy-MM-dd"), resolvedGroups);
    }
}
