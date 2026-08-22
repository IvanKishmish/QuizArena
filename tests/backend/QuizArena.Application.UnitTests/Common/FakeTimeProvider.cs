namespace QuizArena.Application.UnitTests.Common;

/// <summary>
/// A TimeProvider that always reports the same instant. See QuizArena.Domain.UnitTests.Common.FakeTimeProvider
/// for the domain-project twin of this class (kept separate rather than shared, since Application.UnitTests
/// and Domain.UnitTests don't reference each other) — used to make freeze-expiry deterministic without a
/// real wall-clock wait (S13).
/// </summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
