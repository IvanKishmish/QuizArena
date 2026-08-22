using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.GameHistory.Events;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.Features.GameHistory.Commands.SaveGameHistory;

public sealed class SaveGameHistoryCommandHandler(
    IAppDbContext context, IIdentityService identityService, IOutboxWriter outboxWriter)
: ICommandHandler<SaveGameHistoryCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(SaveGameHistoryCommand command, CancellationToken ct = default)
    {
        var registeredUserIds = command.ParticipantUserIds.Values
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var players = registeredUserIds.Count == 0
            ? []
            : await context.Players.Where(p => registeredUserIds.Contains(p.Id)).ToListAsync(ct);

        var playersById = players.ToDictionary(p => p.Id);

        // Defensive: IIdentityService.GetEmailsAsync isn't contractually guaranteed to return non-null
        // (and an unconfigured test double for it returns null too) — treat a null result the same as
        // "no emails known" rather than letting it blow up the whole history-save with an NRE.
        var emailsByUserId = registeredUserIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityService.GetEmailsAsync(registeredUserIds, ct) ?? new Dictionary<Guid, string>();

        for (var i = 0; i < command.FinalLeaderboard.Count; i++)
        {
            var entry = command.FinalLeaderboard[i];
            var placement = i + 1;
            var participantUserId = command.ParticipantUserIds.GetValueOrDefault(entry.ParticipantId);

            var creationParams = new GameHistoryEntryCreationParams(
                command.GameId,
                command.QuizSetId,
                participantUserId,
                entry.DisplayName,
                (int)entry.Score,
                placement);

            var historyResult = GameHistoryEntry.Create(creationParams);

            if (historyResult.IsError)
                continue;

            context.GameHistory.Add(historyResult.Value);

            if (participantUserId is not null && playersById.TryGetValue(participantUserId.Value, out var player))
                player.RecordGameResult((int)entry.Score);

            if (participantUserId is not null && emailsByUserId.TryGetValue(participantUserId.Value, out var email)
                                               && !string.IsNullOrWhiteSpace(email))
            {
                outboxWriter.Enqueue(new GameResultsEmailMessage(
                    email, entry.DisplayName, (int)entry.Score, placement, IsWinner: placement == 1));
            }
        }

        await context.SaveChangesAsync(ct);

        return Result.Success;
    }
}