using FluentValidation;
using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;

namespace QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;

public sealed class JoinGameRoomCommandHandler(
    IGameRoomStore gameRoomStore,
    ICurrentUserService currentUser,
    IValidator<JoinGameRoomCommand> validator)
: ICommandHandler<JoinGameRoomCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(JoinGameRoomCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var gameRoom = await gameRoomStore.GetByRoomCodeAsync(command.RoomCode, ct);
        
        if (gameRoom is null)
            return Error.NotFound("GameRoom.NotFound", "Room not found.");
        
        Guid? userId = currentUser.UserId;
        Guid? guestId = userId is null ? Guid.CreateVersion7() : null;
        
        var addResult = gameRoom.AddParticipant(userId, guestId, command.DisplayName);

        if (addResult.IsError)
            return addResult.Errors;
        
        await gameRoomStore.SaveAsync(gameRoom, ct);

        var participant = gameRoom.Participants.Last();

        return participant.Id;
    }
}