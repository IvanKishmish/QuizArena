using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.GameRooms.Commands.StartGame;

public sealed class StartGameCommandHandler(
    IGameRoomStore gameRoomStore,
    ICurrentUserService currentUser,
    IValidator<StartGameCommand> validator)
: ICommandHandler<StartGameCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(StartGameCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var hostId = currentUser.UserId.Value;

        return await OptimisticConcurrency.ExecuteAsync(gameRoomStore, command.RoomCode, (gameRoom, _) =>
        {
            if (gameRoom.HostId != hostId)
                return Task.FromResult<ErrorOr<Updated>>(
                    Error.Forbidden("GameRoom.NotHost", "Only the host can start the game."));

            return Task.FromResult(gameRoom.Start());
        }, ct);
    }
}
