using MongoDB.Driver;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;
using QuizArena.Persistence.Mongo.Documents;

namespace QuizArena.Persistence.Mongo;

public sealed class QuestionStore(IMongoDatabase db) : IQuestionStore
{
    private readonly IMongoCollection<QuestionDocument> _collection
        = db.GetCollection<QuestionDocument>("questions");

    public async Task InsertAsync(Guid quizSetId, Question question, CancellationToken ct = default)
    {
        var document = question.ToDocument(quizSetId);

        await _collection.InsertOneAsync(document, cancellationToken: ct);
    }

    public async Task<List<Question>> GetByQuizSetIdAsync(Guid quizSetId, CancellationToken ct = default)
    {
        var documents = await _collection
            .Find(d => d.QuizSetId == quizSetId)
            .ToListAsync(ct);

        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task<Question?> GetByIdAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default)
    {
        var document = await _collection
            .Find(d => d.QuizSetId == quizSetId && d.Id == questionId)
            .FirstOrDefaultAsync(ct);

        return document?.ToDomain();
    }

    public async Task DeleteAsync(Guid quizSetId, Guid questionId, CancellationToken ct = default)
        => await _collection.DeleteOneAsync(d => d.QuizSetId == quizSetId && d.Id == questionId, ct);

    public async Task DeleteByQuizSetIdAsync(Guid quizSetId, CancellationToken ct = default)
        => await _collection.DeleteManyAsync(q => q.QuizSetId == quizSetId, ct);
}
