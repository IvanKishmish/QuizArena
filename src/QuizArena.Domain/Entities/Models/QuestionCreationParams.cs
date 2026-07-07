using QuizArena.Domain.Enums;

namespace QuizArena.Domain.Entities.Models;

public sealed record QuestionCreationParams(
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionParams> Options);