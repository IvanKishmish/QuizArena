using ErrorOr;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.GameRooms.Commands.StartGame;

public sealed class StartGameCommandHandler(
    IGameRoomStore gameRoomStore,
    ICurrentUserService currentUser)
: ICommandHandler<StartGameCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(StartGameCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);

        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");

        if (gameRoom.HostId != currentUser.UserId)
            return Error.Forbidden("GameRoom.NotHost", "Only the host can start the game.");

        var startResult = gameRoom.Start();

        if (startResult.IsError)
            return startResult.Errors;
        
        await gameRoomStore.SaveAsync(gameRoom, ct);

        return Result.Updated;
    }
}