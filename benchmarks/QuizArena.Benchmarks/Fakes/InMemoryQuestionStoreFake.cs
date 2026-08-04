using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Benchmarks.Fakes;

public sealed class InMemoryQuestionStoreFake(Question fixedQuestion) : IQuestionStore
{
    public Task InsertAsync(Guid quizSetId, Question question, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<List<Question>> GetByQuizSetIdAsync(Guid quizSetId, CancellationToken ct = default)
        => Task.FromResult(new List<Question> { fixedQuestion });

    public Task<Question?> GetByIdAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default)
        => Task.FromResult<Question?>(fixedQuestion);

    public Task DeleteAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteByQuizSetIdAsync(Guid quizSetId, CancellationToken ct = default)
        => Task.CompletedTask;
}