using EnrichedMessaging.Domain;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Handlers;

public sealed class MessageDeletedPollCleanupHandler : IIntegrationEventHandler<MessageDeletedIntegrationEvent>
{
    private readonly IPollRepository _polls;

    public MessageDeletedPollCleanupHandler(IPollRepository polls)
    {
        _polls = polls;
    }

    public async Task HandleAsync(MessageDeletedIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        await _polls.DeleteByMessageIdAsync(evt.MessageId, cancellationToken);
    }
}
