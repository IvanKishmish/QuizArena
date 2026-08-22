using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Features.GameHistory.Commands.SaveGameHistory;
using QuizArena.Application.Features.GameRooms.Events;

namespace QuizArena.Application.Features.GameRooms.Commands.EndGame;

public sealed class EndGameCommandHandler(
    IGameRoomStore gameRoomStore,
    ILeaderboardStore leaderboardStore,
    IMediator mediator,
    ICurrentUserService currentUser,
    IValidator<EndGameCommand> validator)
    : ICommandHandler<EndGameCommand, ErrorOr<Updated>>
{
    private sealed record EndGameOutcome(Guid GameId, Guid QuizSetId, IReadOnlyDictionary<Guid, Guid?> ParticipantUserIds);

    public async ValueTask<ErrorOr<Updated>> Handle(EndGameCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var hostId = currentUser.UserId.Value;

        var result = await OptimisticConcurrency.ExecuteAsync(gameRoomStore, command.RoomCode, (gameRoom, _) =>
        {
            if (gameRoom.HostId != hostId)
                return Task.FromResult<ErrorOr<EndGameOutcome>>(
                    Error.Forbidden("GameRoom.NotHost", "Only the host can end the game."));

            var finishResult = gameRoom.Finish();

            if (finishResult.IsError)
                return Task.FromResult<ErrorOr<EndGameOutcome>>(finishResult.Errors);

            var participantUserIds = gameRoom.Participants.ToDictionary(p => p.Id, p => p.UserId);

            return Task.FromResult<ErrorOr<EndGameOutcome>>(new EndGameOutcome(gameRoom.Id, gameRoom.QuizSetId, participantUserIds));
        }, ct);

        if (result.IsError)
            return result.Errors;

        var outcome = result.Value;

        var finalLeaderboard = await leaderboardStore
            .GetTopAsync(command.RoomCode, outcome.ParticipantUserIds.Count, ct);

        // Sent as a direct Command, not as another GameFinishedNotification subscriber: writing game history
        // (and, via it, updating Player stats — see SaveGameHistoryCommandHandler / W6) has to succeed before
        // we tell anyone the game is over, so its failure surfaces to the caller instead of silently racing
        // against CleanupGameRoomHandler like the notification handlers do.
        var saveHistoryResult = await mediator.Send(
            new SaveGameHistoryCommand(outcome.GameId, outcome.QuizSetId, finalLeaderboard, outcome.ParticipantUserIds), ct);

        if (saveHistoryResult.IsError)
            return saveHistoryResult.Errors;

        await mediator.Publish(
            new GameFinishedNotification(command.RoomCode, outcome.QuizSetId, finalLeaderboard, outcome.ParticipantUserIds), ct);

        return Result.Updated;
    }
}
