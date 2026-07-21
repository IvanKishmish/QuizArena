using FluentValidation;
using QuizArena.Application.Features.QuizSets.Commands.Common;

namespace QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;

public sealed class CreateQuizSetCommandValidator : AbstractValidator<CreateQuizSetCommand>
{
    public CreateQuizSetCommandValidator()
    {
        RuleFor(x => x.Title).QuizSetTitle();

        RuleFor(x => x.Description).QuizSetDescription();
    }
}