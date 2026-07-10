using FluentValidation;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using Mediator;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;

public sealed class CreateQuizSetCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IValidator<CreateQuizSetCommand> validator)
: ICommandHandler<CreateQuizSetCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateQuizSetCommand command, CancellationToken ct = default)
    {
        if(currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        var validationResult = await validator.ValidateAsync(command, ct);
        
        if(!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var quizSetCreationParams = new QuizSetCreationParams(
            currentUser.UserId.Value,
            command.Title,
            command.Description);
        
        var quizSetResult = QuizSet.Create(quizSetCreationParams);

        if (quizSetResult.IsError)
            return quizSetResult.Errors;
        
        dbContext.QuizSets.Add(quizSetResult.Value);
        await dbContext.SaveChangesAsync(ct);

        return quizSetResult.Value.Id;
    }
}