namespace TelegramBot.Application.Contracts.Api;

public enum QuestionType
{
    SingleChoice = 0,
    MultipleChoice = 1,
    TrueFalse = 2,
    Ordering = 3
}

public sealed record AnswerOptionRequest(string Text, bool IsCorrect, int OrderIndex);

public sealed record AddQuestionRequest(
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionRequest> Options);

public sealed record AnswerOptionResponse(string Text, int OrderIndex);

public sealed record QuestionResponse(
    Guid Id,
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionResponse> Options);
