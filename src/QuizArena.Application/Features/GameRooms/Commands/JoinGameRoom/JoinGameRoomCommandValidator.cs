using FluentValidation;

namespace QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;

public sealed class JoinGameRoomCommandValidator : AbstractValidator<JoinGameRoomCommand>
{
    public JoinGameRoomCommandValidator()
    {
        RuleFor(x => x.RoomCode)
            .NotEmpty().WithMessage("Room code is required")
            .Length(6).WithMessage("Room code must be 6 characters long");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(30).WithMessage("Display name must not exceed 30 characters");
    }
}