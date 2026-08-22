using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace QuizArena.WebApi.Hubs;

public sealed class HubRateLimitingFilter : IHubFilter
{
    private const int PermitsPerWindow = 30;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<string, FixedWindowRateLimiter> LimitersByConnection = new();

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var limiter = LimitersByConnection.GetOrAdd(
            invocationContext.Context.ConnectionId,
            _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = Window,
                QueueLimit = 0
            }));

        using var lease = await limiter.AcquireAsync(1, invocationContext.Context.ConnectionAborted);

        if (!lease.IsAcquired)
            throw new HubException("Too many requests. Please slow down.");

        return await next(invocationContext);
    }

    public Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        if (LimitersByConnection.TryRemove(context.Context.ConnectionId, out var limiter))
            limiter.Dispose();

        return next(context, exception);
    }
}
