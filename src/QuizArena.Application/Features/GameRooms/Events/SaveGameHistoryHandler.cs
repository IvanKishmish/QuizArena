using Mediator;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.Features.GameRooms.Events;

public sealed class SaveGameHistoryHandler(IAppDbContext context, IGameRoomStore gameRoomStore)
: INotificationHandler<GameFinishedNotification>
{
    public async ValueTask Handle(GameFinishedNotification notification, CancellationToken ct = default)
    {
        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(notification.RoomCode, ct);

        if (gameRoom is null)
            return;

        for (var i = 0; i < notification.FinalLeaderboard.Count; i++)
        {
            var entry = notification.FinalLeaderboard[i];

            var creationParams = new GameHistoryEntryCreationParams(
                notification.QuizSetId,
                notification.ParticipantUserIds.GetValueOrDefault(entry.ParticipantId),
                entry.DisplayName,
                (int)entry.Score,
                Placement: i + 1);

            var historyResult = GameHistoryEntry.Create(creationParams);

            if (historyResult.IsError)
                continue;

            context.GameHistory.Add(historyResult.Value);
        }

        await context.SaveChangesAsync(ct);
    }
}