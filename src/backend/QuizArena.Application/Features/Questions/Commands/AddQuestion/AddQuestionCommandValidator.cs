using FluentValidation;

namespace QuizArena.Application.Features.Questions.Commands.AddQuestion;

public sealed class AddQuestionCommandValidator : AbstractValidator<AddQuestionCommand>
{
    public AddQuestionCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Question text is required")
            .MaximumLength(500).WithMessage("Question text must not exceed 500 characters");

        RuleFor(x => x.TimeLimitSeconds)
            .GreaterThan(0).WithMessage("Time limit must be greater than zero");

        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("Points must be greater than zero");

        RuleFor(x => x.Options)
            .NotEmpty().WithMessage("At least one answer option is required");
    }
}