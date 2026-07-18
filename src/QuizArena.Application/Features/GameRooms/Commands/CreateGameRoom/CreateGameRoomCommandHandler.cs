using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.Features.GameRooms.Commands.CreateGameRoom;

public sealed class CreateGameRoomCommandHandler(
    IAppDbContext context,
    ICurrentUserService currentUser,
    IRoomCodeGenerator roomCodeGenerator,
    IGameRoomStore gameRoomStore)
: ICommandHandler<CreateGameRoomCommand, ErrorOr<string>>
{
    public async ValueTask<ErrorOr<string>> Handle(CreateGameRoomCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);
        
        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");

        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var roomCode = await roomCodeGenerator.GenerateUniqueCodeAsync(ct);

        var creationParams = new GameRoomCreationParams(roomCode, command.QuizSetId, currentUser.UserId.Value);

        var gameRoomResult = GameRoom.Create(creationParams);

        if (gameRoomResult.IsError)
            return gameRoomResult.Errors;
        
        await gameRoomStore.SaveAsync(gameRoomResult.Value, ct);

        return roomCode;
    }
}