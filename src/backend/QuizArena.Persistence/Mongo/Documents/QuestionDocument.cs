using QuizArena.Domain.Enums;

namespace QuizArena.Persistence.Mongo.Documents;

public sealed record QuestionDocument
{
    public required Guid Id { get; init; }
    public required Guid QuizSetId { get; init; }
    public required string Text { get; init; }
    public required QuestionType QuestionType { get; init; }
    public required int TimeLimitSeconds { get; init; }
    public required int Points { get; init; }
    public required List<AnswerOptionDocument> Options { get; init; }
}
