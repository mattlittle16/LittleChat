using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Shared.Infrastructure;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly Dictionary<Type, List<Type>> _handlers = new();

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlerTypes))
            return;

        using var scope = _serviceProvider.CreateScope();
        foreach (var handlerType in handlerTypes)
        {
            try
            {
                var handler = (IIntegrationEventHandler<TEvent>)scope.ServiceProvider.GetRequiredService(handlerType);
                await handler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling integration event {EventType} with handler {HandlerType}",
                    eventType.Name, handlerType.Name);
                throw;
            }
        }
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlerTypes))
        {
            handlerTypes = [];
            _handlers[eventType] = handlerTypes;
        }

        var handlerType = typeof(THandler);
        if (!handlerTypes.Contains(handlerType))
            handlerTypes.Add(handlerType);
    }
}
