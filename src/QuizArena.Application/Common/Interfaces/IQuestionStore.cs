using QuizArena.Domain.Entities;

namespace QuizArena.Application.Common.Interfaces;

public interface IQuestionStore
{
    Task InsertAsync(Guid quizSetId, Question question, CancellationToken ct = default);
    Task<List<Question>> GetByQuizSetIdAsync(Guid quizSetId, CancellationToken ct = default);
    Task<Question?> GetByIdAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default);
    Task DeleteAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default);
}