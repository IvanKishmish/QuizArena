using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Benchmarking.H1;

public sealed class SubmitAnswerInputValidator : AbstractValidator<SubmitAnswerInput>
{
    public SubmitAnswerInputValidator()
    {
        RuleFor(x => x.RoomCode).SetRoomCode();

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("ParticipantId is required");

        RuleFor(x => x.QuestionId)
            .NotEmpty().WithMessage("QuestionId is required");

        RuleFor(x => x.SelectedOptionIndices)
            .NotNull().WithMessage("Selected options are required");
    }
}
