using QuizArena.Application.Features.Questions.Commands.AddQuestion;
using QuizArena.Domain.Enums;

namespace QuizArena.WebApi.Contracts.QuizSets;

public sealed record AddQuestionRequest(
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionDto> Options);