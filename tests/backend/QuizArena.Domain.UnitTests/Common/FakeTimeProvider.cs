namespace QuizArena.Domain.UnitTests.Common;

/// <summary>
/// A TimeProvider that always reports the same instant. Used to make time-dependent domain behavior
/// (freeze expiry, etc.) deterministic in tests without a real wall-clock wait — see S13 in the review
/// response for the flaky Thread.Sleep-based test this replaces.
/// </summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
