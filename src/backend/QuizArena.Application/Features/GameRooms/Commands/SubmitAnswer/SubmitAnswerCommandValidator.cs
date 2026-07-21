using FluentValidation;
using QuizArena.Application.Features.GameRooms.Commands.Common;

namespace QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;

public sealed class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
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