using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Queries;

public sealed class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, MessagePage>
{
    private readonly IMessageRepository _messages;

    public GetMessagesQueryHandler(IMessageRepository messages)
    {
        _messages = messages;
    }

    public async Task<MessagePage> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var isMember = await _messages.IsMemberAsync(request.RoomId, request.UserId, cancellationToken);
        if (!isMember)
            throw new UnauthorizedAccessException("User is not a member of this room.");

        var page = await _messages.GetPageAsync(
            request.RoomId, request.Before, request.BeforeId, request.Limit, cancellationToken);

        // Mark room as read up to now
        await _messages.UpdateLastReadAtAsync(request.RoomId, request.UserId, cancellationToken);

        return page;
    }
}
