using ErrorOr;
using FluentValidation;
using Mediator;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;

public sealed class UpdateQuizSetDetailsCommandHandler
(IAppDbContext context,
    ICurrentUserService currentUser,
    IValidator<UpdateQuizSetDetailsCommand> validator)
: ICommandHandler<UpdateQuizSetDetailsCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(UpdateQuizSetDetailsCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
                    return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        var validationResult = await validator.ValidateAsync(command, ct);
        
        if(!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();
        
        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var updateResult = quizSet.UpdateDetails(command.Title, command.Description);

        if (updateResult.IsError)
            return updateResult.Errors;
        
        await context.SaveChangesAsync(ct);

        return Result.Updated;
    }
}