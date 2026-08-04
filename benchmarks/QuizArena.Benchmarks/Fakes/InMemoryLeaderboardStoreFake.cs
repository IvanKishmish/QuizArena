using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Benchmarks.Fakes;

public sealed class InMemoryLeaderboardStoreFake : ILeaderboardStore
{
    private static readonly IReadOnlyList<LeaderboardEntry> Empty = [];

    public Task UpdateScoreAsync(string roomCode, Guid participantId, string displayName, double score,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string roomCode, int count, CancellationToken ct = default)
        => Task.FromResult(Empty);

    public Task DeleteAsync(string roomCode, CancellationToken ct = default)
        => Task.CompletedTask;
}
