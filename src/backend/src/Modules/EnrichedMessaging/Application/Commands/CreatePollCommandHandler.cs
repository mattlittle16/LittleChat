using EnrichedMessaging.Domain;
using MediatR;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Commands;

public sealed record CreatePollCommand(
    Guid RoomId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Question,
    IReadOnlyList<string> Options,
    string VoteMode
) : IRequest<CreatePollResult>;

public sealed record CreatePollResult(Guid MessageId, Guid PollId);

public sealed class CreatePollCommandHandler : IRequestHandler<CreatePollCommand, CreatePollResult>
{
    private readonly IPollRepository _polls;
    private readonly IEventBus _eventBus;

    public CreatePollCommandHandler(IPollRepository polls, IEventBus eventBus)
    {
        _polls = polls;
        _eventBus = eventBus;
    }

    public async Task<CreatePollResult> Handle(CreatePollCommand request, CancellationToken cancellationToken)
    {
        if (request.Options.Count < 2 || request.Options.Count > 10)
            throw new InvalidOperationException("Polls must have between 2 and 10 options.");

        if (request.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 200))
            throw new InvalidOperationException("Each poll option must be 1–200 characters.");

        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 500)
            throw new InvalidOperationException("Poll question must be 1–500 characters.");

        if (request.VoteMode != "single" && request.VoteMode != "multi")
            throw new InvalidOperationException("Vote mode must be 'single' or 'multi'.");

        var isMember = await _polls.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var messageId = Guid.NewGuid();
        var pollId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Insert message row with message_type='poll' via repository (raw Npgsql in Infrastructure)
        await _polls.CreatePollMessageAsync(messageId, request.RoomId, request.UserId, request.Question, now, cancellationToken);

        // Create poll + options via EF
        var poll = new Poll(pollId, messageId, request.Question, request.VoteMode, [], now);
        var options = request.Options
            .Select((text, idx) => new PollOption(Guid.NewGuid(), pollId, text, idx, 0, []))
            .ToList();

        await _polls.CreateAsync(poll, options, cancellationToken);

        var pollDataDto = new PollDataDto(
            pollId,
            request.Question,
            request.VoteMode,
            options.Select(o => new PollOptionDto(o.Id, o.Text, o.DisplayOrder, 0, [])).ToList(),
            []
        );

        await _eventBus.PublishAsync(new MessageSentIntegrationEvent
        {
            MessageId   = messageId,
            RoomId      = request.RoomId,
            UserId      = request.UserId,
            DisplayName = request.DisplayName,
            AvatarUrl   = request.AvatarUrl,
            Content     = request.Question,
            Attachments = [],
            CreatedAt   = now,
            IsSystem    = false,
            MessageType = "poll",
            PollData    = pollDataDto,
        }, cancellationToken);

        return new CreatePollResult(messageId, pollId);
    }
}
