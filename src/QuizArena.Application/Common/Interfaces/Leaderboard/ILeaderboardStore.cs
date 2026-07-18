namespace QuizArena.Application.Common.Interfaces.Leaderboard;

public interface ILeaderboardStore
{
    Task UpdateScoreAsync(string roomCode, Guid participantId, string displayName, double score,
        CancellationToken ct = default);
    
    Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string roomCode, int count, CancellationToken ct = default);
    
    Task DeleteAsync(string roomCode, CancellationToken ct = default);
}