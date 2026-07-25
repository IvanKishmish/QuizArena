using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;

public sealed class GetQuestionsByQuizSetQueryHandler
(
    IAppDbContext context,
    IQuestionStore questionStore,
    ICurrentUserService currentUser)
: IQueryHandler<GetQuestionsByQuizSetQuery, ErrorOr<IReadOnlyList<QuestionResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<QuestionResponse>>> Handle(
        GetQuestionsByQuizSetQuery query, CancellationToken ct = default)
    {
        if(currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        var quizSet = await context.QuizSets.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == query.QuizSetId, ct);
        
        if (quizSet is null)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
        if (quizSet.OwnerId != currentUser.UserId)
            return Error.Forbidden("QuizSet.NotOwner", "You are not the owner of this quiz set.");
        
        var questions = await questionStore.GetByQuizSetIdAsync(query.QuizSetId, ct);

        return questions
            .Select(q => new QuestionResponse(
                q.Id,
                q.Text,
                q.QuestionType,
                q.TimeLimitSeconds,
                q.Points,
                q.Options.Select(o => new AnswerOptionResponse(o.Text, o.IsCorrect, o.OrderIndex)).ToList()))
            .ToList();
    }
}