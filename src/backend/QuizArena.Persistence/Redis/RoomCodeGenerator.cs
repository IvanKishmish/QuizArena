using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.Redis;

public sealed class RoomCodeGenerator(IGameRoomStore gameRoomStore) : IRoomCodeGenerator
{
    public async Task<string> GenerateUniqueCodeAsync(CancellationToken ct = default)
    {
        string code;
        GameRoom? existing;

        do
        {
            code = Random.Shared.Next(100000, 999999).ToString();
            existing = await gameRoomStore.GetByRoomCodeAsync(code, ct);
        } while (existing is not null);

        return code;
    }
}