using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Features.GameRooms.Commands.NextQuestion;

public sealed class NextQuestionCommandValidator : AbstractValidator<NextQuestionCommand>
{
    public NextQuestionCommandValidator()
    {
        RuleFor(x => x.RoomCode).SetRoomCode();
    }
}