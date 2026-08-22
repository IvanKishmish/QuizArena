using FluentValidation;

namespace QuizArena.Application.Common;

public abstract class PagedQueryValidator<TQuery> : AbstractValidator<TQuery> where TQuery : IPagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
