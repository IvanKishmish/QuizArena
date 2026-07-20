using FluentValidation;

namespace QuizArena.Application.Features.GameRooms.Commands.Common;

public static class RoomCodeValidationRule
{
    public static IRuleBuilderOptions<T, string> SetRoomCode<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Room code is required")
            .Length(6).WithMessage("Room code must be 6 characters long");
}