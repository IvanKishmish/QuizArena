using FluentValidation;

namespace QuizArena.Application.Features.QuizSets.Commands.Common;

public static class QuizSetValidationRules
{
    public static IRuleBuilderOptions<T, string> QuizSetTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");
    
    public static IRuleBuilderOptions<T, string> QuizSetDescription<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");
}