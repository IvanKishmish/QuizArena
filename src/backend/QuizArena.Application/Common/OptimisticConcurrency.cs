using ErrorOr;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Common;

public static class OptimisticConcurrency
{
    private const int MaxAttempts = 8;

    public static async Task<ErrorOr<TResult>> ExecuteAsync<TResult>(
        IGameRoomStore store,
        string roomCode,
        Func<GameRoom, CancellationToken, Task<ErrorOr<TResult>>> mutateAsync,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var gameRoom = await store.GetByRoomCodeAsync(roomCode, ct);

            if (gameRoom is null)
                return Error.NotFound("GameRoom.NotFound", "Game room not found.");

            var result = await mutateAsync(gameRoom, ct);

            if (result.IsError)
                return result;

            if (await store.SaveAsync(gameRoom, ct))
                return result;

            await Task.Delay(Random.Shared.Next(10, 30) * attempt, ct);
        }

        return Error.Conflict(
            "GameRoom.ConcurrencyConflict",
            "The room state changed too many times while processing this request. Please try again.");
    }
}
