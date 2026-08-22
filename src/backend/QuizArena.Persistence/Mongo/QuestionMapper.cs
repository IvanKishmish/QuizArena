using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Persistence.Mongo.Documents;

namespace QuizArena.Persistence.Mongo;

public static class QuestionMapper
{
    public static QuestionDocument ToDocument(this Question question, Guid quizSetId) => new()
    {
        Id = question.Id,
        QuizSetId = quizSetId,
        Text = question.Text,
        QuestionType = question.QuestionType,
        TimeLimitSeconds = question.TimeLimitSeconds,
        Points = question.Points,
        Options = question.Options
            .Select(o => new AnswerOptionDocument
            {
                Text = o.Text,
                IsCorrect = o.IsCorrect,
                OrderIndex = o.OrderIndex
            })
            .ToList()
    };

    public static Question ToDomain(this QuestionDocument document)
    {
        var options = document.Options
            .Select(o => new AnswerOptionParams(o.Text, o.IsCorrect, o.OrderIndex))
            .ToList();

        var creationParams = new QuestionCreationParams(
            document.Text,
            document.QuestionType,
            document.TimeLimitSeconds,
            document.Points,
            options);

        // Bypasses Question.Create()'s validation on purpose: this data was already validated once, when
        // the question was first created, and re-validating on every read would just be wasted work (and
        // would turn "we changed a validation rule" into "old data silently fails to load").
        return new Question(document.Id, creationParams);
    }
}
