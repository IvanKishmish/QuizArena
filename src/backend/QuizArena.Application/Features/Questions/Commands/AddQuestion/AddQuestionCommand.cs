using ErrorOr;
using Mediator;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.Questions.Commands.AddQuestion;

public sealed record AddQuestionCommand(
    Guid QuizSetId,
    string Text,
    QuestionType QuestionType,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<AnswerOptionDto> Options) : ICommand<ErrorOr<Guid>>;