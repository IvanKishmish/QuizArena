using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;

public sealed class UsePowerUpCommandValidator : AbstractValidator<UsePowerUpCommand>
{
    public UsePowerUpCommandValidator()
    {
        RuleFor(x => x.RoomCode).SetRoomCode();

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("ParticipantId is required");

        RuleFor(x => x.PowerUpType)
            .IsInEnum().WithMessage("Unknown power-up type");
    }
}