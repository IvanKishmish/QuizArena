namespace QuizArena.Application.Features.Questions.Commands.AddQuestion;

public sealed record AnswerOptionDto(
    string Text,
    bool IsCorrect,
    int OrderIndex);