using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Benchmarks.Fakes;

public sealed class InMemoryGameRoomStoreFake(GameRoom fixedRoom) : IGameRoomStore
{
    public async Task SaveAsync(GameRoom gameRoom, CancellationToken ct = default)
        => await Task.CompletedTask;

    public async Task<GameRoom?> GetByRoomCodeAsync(string roomCode, CancellationToken ct = default)
        => await Task.FromResult<GameRoom?>(fixedRoom);

    public async Task DeleteAsync(string roomCode, CancellationToken ct = default)
        => await Task.CompletedTask;
}