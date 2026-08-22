using System.Text.Json;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Persistence.Redis.Documents;
using StackExchange.Redis;

namespace QuizArena.Persistence.Redis;

public sealed class GameRoomStore(IConnectionMultiplexer redis) : IGameRoomStore
{
    private static readonly TimeSpan RoomExpiration = TimeSpan.FromHours(4);

    private static readonly LuaScript CompareAndSwapScript = LuaScript.Prepare(
        """
        local current = redis.call('GET', @key)
        local expectedVersion = tonumber(@expectedVersion)

        if expectedVersion == 0 then
            if current then
                return 0
            end
        else
            if not current then
                return 0
            end

            local ok, decoded = pcall(cjson.decode, current)
            if not ok or tonumber(decoded['Version']) ~= expectedVersion then
                return 0
            end
        end

        redis.call('SET', @key, @newValue, 'EX', @ttlSeconds)
        return 1
        """);

    private IDatabase Database => redis.GetDatabase();

    private static string Key(string roomCode) => $"gameroom:{roomCode}";

    public async Task<bool> SaveAsync(GameRoom gameRoom, CancellationToken ct = default)
    {
        var expectedVersion = gameRoom.Version;
        var snapshot = gameRoom.ToSnapshot() with { Version = expectedVersion + 1 };
        var json = JsonSerializer.Serialize(snapshot);

        var result = await Database.ScriptEvaluateAsync(CompareAndSwapScript, new
        {
            key = (RedisKey)Key(gameRoom.RoomCode),
            expectedVersion,
            newValue = json,
            ttlSeconds = (int)RoomExpiration.TotalSeconds
        });

        return (long)result == 1;
    }

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode, CancellationToken ct = default)
    {
        var json = await Database.StringGetAsync(Key(roomCode));
        
        if(json.IsNullOrEmpty)
            return null;
        
        var snapshot = JsonSerializer.Deserialize<GameRoomSnapshot>((string)json!);

        return snapshot?.ToDomain();
    }

    public Task DeleteAsync(string roomCode, CancellationToken ct = default)
        =>  Database.KeyDeleteAsync(Key(roomCode)); 
}