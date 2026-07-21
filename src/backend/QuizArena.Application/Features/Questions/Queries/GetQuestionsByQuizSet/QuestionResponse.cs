using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;

public sealed record QuestionResponse(
    Guid Id,
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionResponse> Options);