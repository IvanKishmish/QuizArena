using System.Net;
using Polly;
using Polly.Extensions.Http;

namespace TelegramBot.Infrastructure.Api;

/// <summary>
/// Network failures against the backend are expected (container restarts, deploys,
/// transient DNS blips in Docker Compose) — this is why Part 2 of the spec calls out
/// Polly explicitly. Two policies, combined at registration:
///   1. Retry: 3 attempts, exponential backoff with jitter, only for transient statuses
///      (408, 5xx) and network exceptions — never for 4xx, which are real client errors.
///   2. Circuit breaker: after 5 consecutive failures, stop hammering a downed backend
///      for 30s and fail fast instead, so one broken dependency doesn't pile up latency
///      across every chat's requests.
/// </summary>
public static class PollyPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx and 408
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)) +
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)));

    public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
