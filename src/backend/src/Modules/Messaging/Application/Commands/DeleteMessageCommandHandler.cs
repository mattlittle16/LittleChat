using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class DeleteMessageCommandHandler : IRequestHandler<DeleteMessageCommand>
{
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public DeleteMessageCommandHandler(IMessageRepository messages, IEventBus eventBus)
    {
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
    {
        await _messages.DeleteAsync(request.MessageId, request.UserId, cancellationToken);

        await _eventBus.PublishAsync(new MessageDeletedIntegrationEvent
        {
            MessageId = request.MessageId,
            RoomId = request.RoomId,
        }, cancellationToken);
    }
}
