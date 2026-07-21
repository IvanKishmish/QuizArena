using FluentValidation;
using QuizArena.Application.Features.QuizSets.Commands.Common;

namespace QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;

public sealed class UpdateQuizSetDetailsCommandValidator : AbstractValidator<UpdateQuizSetDetailsCommand>
{
    public UpdateQuizSetDetailsCommandValidator()
    {
        RuleFor(x => x.Title).QuizSetTitle();

        RuleFor(x => x.Description).QuizSetDescription();
    }
}