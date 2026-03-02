using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Interfaces;

namespace RealTime.API;

public static class RealTimeModuleExtensions
{
    public static IServiceCollection AddRealTimeModule(this IServiceCollection services)
    {
        // SignalRRealtimeNotifier registered here (lives in RealTime.API alongside ChatHub)
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        // SubClaimUserIdProvider registered in Program.cs (composition root) since it's in Infrastructure
        return services;
    }
}
