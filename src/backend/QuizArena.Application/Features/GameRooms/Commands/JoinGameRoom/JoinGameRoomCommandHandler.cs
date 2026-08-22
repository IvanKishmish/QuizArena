using FluentValidation;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;

public sealed class JoinGameRoomCommandHandler(
    IGameRoomStore gameRoomStore,
    ICurrentUserService currentUser,
    IParticipantTokenService participantTokenService,
    IValidator<JoinGameRoomCommand> validator)
: ICommandHandler<JoinGameRoomCommand, ErrorOr<JoinGameRoomResult>>
{
    private sealed record JoinOutcome(Guid ParticipantId, Guid? UserId);

    public async ValueTask<ErrorOr<JoinGameRoomResult>> Handle(JoinGameRoomCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var userId = currentUser.UserId;

        var result = await OptimisticConcurrency.ExecuteAsync(
            gameRoomStore, command.RoomCode, (gameRoom, _) => Mutate(gameRoom, userId, command.DisplayName), ct);

        if (result.IsError)
            return result.Errors;

        var token = participantTokenService
            .GenerateToken(command.RoomCode, result.Value.ParticipantId, result.Value.UserId);

        return new JoinGameRoomResult(result.Value.ParticipantId, token);
    }

    private static Task<ErrorOr<JoinOutcome>> Mutate(GameRoom gameRoom, Guid? userId, string displayName)
    {
        if (userId is not null)
        {
            var existingParticipant = gameRoom.Participants.FirstOrDefault(p => p.UserId == userId);

            if (existingParticipant is not null)
                return Task.FromResult<ErrorOr<JoinOutcome>>(new JoinOutcome(existingParticipant.Id, userId));
        }

        Guid? guestId = userId is null ? Guid.CreateVersion7() : null;

        var addResult = gameRoom.AddParticipant(userId, guestId, displayName);

        if (addResult.IsError)
            return Task.FromResult<ErrorOr<JoinOutcome>>(addResult.Errors);

        var createdParticipant = gameRoom.Participants.First(p => p.UserId == userId && p.GuestId == guestId);

        return Task.FromResult<ErrorOr<JoinOutcome>>(new JoinOutcome(createdParticipant.Id, userId));
    }
}
