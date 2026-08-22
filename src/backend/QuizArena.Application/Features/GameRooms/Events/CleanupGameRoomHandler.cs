using Mediator;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Application.Features.GameRooms.Events;

public sealed class CleanupGameRoomHandler(IGameRoomStore gameRoomStore, ILeaderboardStore leaderboardStore)
    : INotificationHandler<GameFinishedNotification>
{
    public async ValueTask Handle(GameFinishedNotification notification, CancellationToken ct)
    {
        var deleteRoom = gameRoomStore.DeleteAsync(notification.RoomCode, ct);
        var deleteLeaderboard = leaderboardStore.DeleteAsync(notification.RoomCode, ct);
        await Task.WhenAll(deleteRoom, deleteLeaderboard);
    }
}