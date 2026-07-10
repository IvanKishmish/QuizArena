using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.Mongo.Documents;

public sealed record QuestionDocument
{
    public required Guid Id { get; init; }
    public required Guid QuizSetId { get; init; }
    public required Question Question { get; init; } = null!;
}