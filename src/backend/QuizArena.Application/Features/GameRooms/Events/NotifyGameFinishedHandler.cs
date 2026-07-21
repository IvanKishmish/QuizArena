using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.GameRooms.Events;

public sealed class NotifyGameFinishedHandler(IGameNotifier gameNotifier)
    : INotificationHandler<GameFinishedNotification>
{
    public async ValueTask Handle(GameFinishedNotification notification, CancellationToken ct)
    {
        await gameNotifier.GameFinishedAsync(notification.RoomCode, notification.FinalLeaderboard, ct);
    }
}