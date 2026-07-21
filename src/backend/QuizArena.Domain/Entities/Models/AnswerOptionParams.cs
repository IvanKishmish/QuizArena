namespace QuizArena.Domain.Entities.Models;

public sealed record AnswerOptionParams(
    string Text,
    bool IsCorrect,
    int OrderIndex);