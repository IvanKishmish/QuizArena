using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Benchmarks.Fakes;

public sealed class InMemoryGameRoomStoreFake(GameRoom fixedRoom) : IGameRoomStore
{
    // K4: IGameRoomStore.SaveAsync now returns bool (a real compare-and-swap outcome in GameRoomStore) — this
    // benchmark fake always reports success, since it isn't exercising K4's concurrency behavior at all.
    public async Task<bool> SaveAsync(GameRoom gameRoom, CancellationToken ct = default)
        => await Task.FromResult(true);

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode, CancellationToken ct = default)
        => await Task.FromResult<GameRoom?>(fixedRoom);

    public async Task DeleteAsync(string roomCode, CancellationToken ct = default)
        => await Task.CompletedTask;
}