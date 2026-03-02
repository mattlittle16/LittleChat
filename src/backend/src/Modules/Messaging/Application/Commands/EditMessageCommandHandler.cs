using MediatR;
using Messaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Messaging.Application.Commands;

public sealed class EditMessageCommandHandler : IRequestHandler<EditMessageCommand>
{
    private readonly IMessageRepository _messages;
    private readonly IEventBus _eventBus;

    public EditMessageCommandHandler(IMessageRepository messages, IEventBus eventBus)
    {
        _messages = messages;
        _eventBus = eventBus;
    }

    public async Task Handle(EditMessageCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 4000)
            throw new InvalidOperationException("Content must be 1–4000 characters.");

        var editedAt = await _messages.EditAsync(
            request.MessageId,
            request.UserId,
            request.Content.Trim(),
            cancellationToken);

        await _eventBus.PublishAsync(new MessageEditedIntegrationEvent
        {
            MessageId = request.MessageId,
            RoomId = request.RoomId,
            Content = request.Content.Trim(),
            EditedAt = editedAt,
        }, cancellationToken);
    }
}
