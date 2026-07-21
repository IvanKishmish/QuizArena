using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using ErrorOr;
using FluentValidation;
using Mediator;
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
    public async ValueTask<ErrorOr<Updated>> Handle(EndGameCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);

        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");

        if (gameRoom.HostId != currentUser.UserId)
            return Error.Forbidden("GameRoom.NotHost", "Only the host can end the game.");

        var finishResult = gameRoom.Finish();

        if (finishResult.IsError)
            return finishResult.Errors;

        var finalLeaderboard = await leaderboardStore.GetTopAsync(command.RoomCode, gameRoom.Participants.Count, ct);

        var participantUserIds = gameRoom.Participants.ToDictionary(p => p.Id, p => p.UserId);

        await mediator.Publish(
            new GameFinishedNotification(command.RoomCode, gameRoom.QuizSetId, finalLeaderboard, participantUserIds), ct);

        return Result.Updated;
    }
}