using ErrorOr;
using Mediator;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;

public sealed record UsePowerUpCommand(
    string RoomCode,
    Guid ParticipantId,
    PowerUpType PowerUpType,
    Guid? TargetParticipantId) : ICommand<ErrorOr<Updated>>;