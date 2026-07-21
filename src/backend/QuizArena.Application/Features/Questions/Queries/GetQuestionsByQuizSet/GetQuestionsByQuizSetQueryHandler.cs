using Mediator;
using QuizArena.Application.Common.Interfaces;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;

public sealed class GetQuestionsByQuizSetQueryHandler
(IAppDbContext context,
    IQuestionStore questionStore)
: IQueryHandler<GetQuestionsByQuizSetQuery, ErrorOr<IReadOnlyList<QuestionResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<QuestionResponse>>> Handle(
        GetQuestionsByQuizSetQuery query, CancellationToken ct = default)
    {
        var quizSetExists = await context.QuizSets
            .AsNoTracking()
            .AnyAsync(q => q.Id == query.QuizSetId, ct);
        
        if (!quizSetExists)
            return Error.NotFound("QuizSet.NotFound", "Quiz set not found.");
        
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