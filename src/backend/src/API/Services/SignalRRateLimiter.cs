using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace API.Services;

/// <summary>
/// Per-user sliding window rate limiter for SignalR hub methods.
/// HTTP endpoints use ASP.NET Core's built-in RateLimiter middleware instead.
/// </summary>
public sealed class SignalRRateLimiter
{
    private readonly int _maxPerMinute;
    private readonly ConcurrentDictionary<string, SlidingWindowRateLimiter> _limiters = new();

    public SignalRRateLimiter(int maxPerMinute)
    {
        _maxPerMinute = maxPerMinute;
    }

    /// <summary>Returns true if the request is within the rate limit; false if it should be rejected.</summary>
    public bool IsAllowed(string userId)
    {
        var limiter = _limiters.GetOrAdd(userId, _ => new SlidingWindowRateLimiter(
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _maxPerMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

        using var lease = limiter.AttemptAcquire();
        return lease.IsAcquired;
    }
}
