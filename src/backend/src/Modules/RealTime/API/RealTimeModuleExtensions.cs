using Microsoft.Extensions.DependencyInjection;

namespace RealTime.API;

public static class RealTimeModuleExtensions
{
    public static IServiceCollection AddRealTimeModule(this IServiceCollection services)
    {
        // SignalRRealtimeNotifier and ChatHub live in the API composition root
        return services;
    }
}
