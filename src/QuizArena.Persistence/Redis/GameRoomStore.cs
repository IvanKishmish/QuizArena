using System.Text.Json;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using StackExchange.Redis;

namespace QuizArena.Persistence.Redis;

public sealed class GameRoomStore(IConnectionMultiplexer redis) : IGameRoomStore
{
    private static readonly TimeSpan RoomExpiration = TimeSpan.FromHours(4);

    private IDatabase Database => redis.GetDatabase();

    private static string Key(string roomCode) => $"gameroom:{roomCode}";
    
    public async Task SaveAsync(GameRoom gameRoom, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(gameRoom);

        await Database.StringSetAsync(Key(gameRoom.RoomCode), json, RoomExpiration);
    }

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode, CancellationToken ct = default)
    {
        var json = await Database.StringGetAsync(Key(roomCode));
        
        if(json.IsNullOrEmpty)
            return null;
        
        return JsonSerializer.Deserialize<GameRoom>((string)json!);
    }

    public async Task DeleteAsync(string roomCode, CancellationToken ct = default)
        =>  await Database.KeyDeleteAsync(Key(roomCode)); 
}