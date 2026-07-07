using ErrorOr;
using QuizArena.Domain.Common;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.Entities;

public sealed class Question : Entity
{
    public string Text { get; private init; } = string.Empty;

    public QuestionType QuestionType { get; private init; }

    public int TimeLimitSeconds { get; private init; }

    public int Points { get; private init; }

    private readonly List<AnswerOptionParams> _options = [];
    public IReadOnlyList<AnswerOptionParams> Options => _options.AsReadOnly();

    private Question()
    { } // ef

    private Question(Guid id, QuestionCreationParams args) : base(id)
    {
        Text = args.Text;
        QuestionType = args.QuestionType;
        TimeLimitSeconds = args.TimeLimitSeconds;
        Points = args.Points;
        _options = args.Options
            .Select(o => new AnswerOptionParams(o.Text, o.IsCorrect, o.OrderIndex))
            .ToList();
    }

    public static ErrorOr<Question> Create(QuestionCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new Question(Guid.CreateVersion7(), args);
    }

    private static ErrorOr<Success> ValidateInvariants(QuestionCreationParams args)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(args.Text))
            errors.Add(Error.Validation("Question.TextRequired", "Question text is required"));

        if (args.TimeLimitSeconds <= 0)
            errors.Add(Error.Validation("Question.InvalidTimeLimit", "Time limit must be greater than zero"));

        if (args.Points <= 0)
            errors.Add(Error.Validation("Question.InvalidPoints", "Points must be greater than zero"));

        if (args.Options.Count == 0)
        {
            errors.Add(Error.Validation("Question.OptionsRequired", "A question must contain at least one answer option"));
            return errors;
        }

        var correctCount = args.Options.Count(o => o.IsCorrect);

        switch (args.QuestionType)
        {
            case QuestionType.SingleChoice:
            case QuestionType.TrueFalse:
                if (correctCount != 1)
                    errors.Add(Error.Validation(
                        "Question.SingleCorrectAnswerRequired",
                        "SingleChoice/TrueFalse questions must have exactly one correct answer option"));
                break;

            case QuestionType.MultipleChoice:
                if (correctCount == 0)
                    errors.Add(Error.Validation(
                        "Question.AtLeastOneCorrectAnswerRequired",
                        "MultipleChoice questions must have at least one correct answer option"));
                break;

            case QuestionType.Ordering:
                if (args.Options.Select(o => o.OrderIndex).Distinct().Count() != args.Options.Count)
                    errors.Add(Error.Validation(
                        "Question.DuplicateOrderIndex",
                        "Ordering questions must have unique order indices for each option"));
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(args.QuestionType), args.QuestionType, "Unhandled question type");
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}