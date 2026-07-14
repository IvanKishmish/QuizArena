using QuizArena.Domain.Entities;

namespace QuizArena.Application.Common.Interfaces;

public interface IGameRoomStore
{
    Task SaveAsync(GameRoom gameRoom, CancellationToken ct = default);
    Task<GameRoom?> GetByRoomCodeAsync(string roomCode, CancellationToken ct = default);
    Task DeleteAsync(string roomCode, CancellationToken ct = default);
}