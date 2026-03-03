using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace RealTime.Application.Handlers;

public sealed class DmCreatedHandler : IIntegrationEventHandler<DmCreatedIntegrationEvent>
{
    private readonly IRealtimeNotifier _notifier;

    public DmCreatedHandler(IRealtimeNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(DmCreatedIntegrationEvent evt, CancellationToken cancellationToken = default)
        => _notifier.SendToUserAsync(
            evt.RecipientUserId.ToString(),
            "DmCreated",
            evt,
            cancellationToken);
}
