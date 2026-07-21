using System.Text.Json;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using StackExchange.Redis;

namespace QuizArena.Persistence.Redis;

public sealed class LeaderboardStore(IConnectionMultiplexer redis) : ILeaderboardStore
{
    private static readonly TimeSpan LeaderboardExpiration = TimeSpan.FromHours(4); 
    
    private IDatabase Database => redis.GetDatabase();

    private static string Key(string roomCode) => $"leaderboard:{roomCode}";
    
    public async Task UpdateScoreAsync(string roomCode, Guid participantId, string displayName, double score,
        CancellationToken ct = default)
    {
        var member = JsonSerializer.Serialize(new { participantId, displayName });

        await Database.SortedSetAddAsync(Key(roomCode), member, score);
        await Database.KeyExpireAsync(Key(roomCode), LeaderboardExpiration);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string roomCode, int count, CancellationToken ct = default)
    {
        var entries = await Database.SortedSetRangeByScoreWithScoresAsync(
            Key(roomCode), order: Order.Descending, take: count);

        return entries.Select(e =>
        {
            var data = JsonSerializer.Deserialize<JsonElement>((string)e.Element!);
            return new LeaderboardEntry(
                data.GetProperty("participantId").GetGuid(),
                data.GetProperty("displayName").GetString()!,
                e.Score);
        }).ToList();
    }

    public async Task DeleteAsync(string roomCode, CancellationToken ct = default)
        => await Database.KeyDeleteAsync(Key(roomCode));
}