namespace QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;

public sealed record AnswerOptionResponse(
    string Text,
    bool IsCorrect,
    int OrderIndex);