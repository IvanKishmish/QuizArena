namespace QuizArena.Persistence.Mongo.Documents;

public sealed record AnswerOptionDocument
{
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
    public required int OrderIndex { get; init; }
}
