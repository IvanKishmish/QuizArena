using FluentValidation;
using QuizArena.Application.Common.Interfaces;
using Mediator;
using ErrorOr;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.Features.Questions.Commands.AddQuestion;

public sealed class AddQuestionCommandHandler(
    IAppDbContext context,
    IQuestionStore questionStore,
    ICurrentUserService currentUser,
    IValidator<AddQuestionCommand> validator)
: ICommandHandler<AddQuestionCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(AddQuestionCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var quizSet = await context.QuizSets.FindAsync([command.QuizSetId], ct);

        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");

        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");

        var optionParams = command.Options
            .Select(o => new AnswerOptionParams(o.Text, o.IsCorrect, o.OrderIndex))
            .ToList();

        var questionParams = new QuestionCreationParams(
            command.Text,
            command.QuestionType,
            command.TimeLimitSeconds,
            command.Points,
            optionParams);

        var questionResult = Question.Create(questionParams);

        if (questionResult.IsError)
            return questionResult.Errors;

        await questionStore.InsertAsync(command.QuizSetId, questionResult.Value, ct);

        return questionResult.Value.Id;
    }
}