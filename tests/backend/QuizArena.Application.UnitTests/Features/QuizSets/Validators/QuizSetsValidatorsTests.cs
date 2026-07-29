using FluentValidation.TestHelper;
using QuizArena.Application.Features.Questions.Commands.AddQuestion;
using QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.QuizSets.Validators;

public class CreateQuizSetCommandValidatorTests
{
    private readonly CreateQuizSetCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyTitle_HasValidationErrorForTitle()
    {
        var command = new CreateQuizSetCommand("", "Description");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleExceedingMaxLength_HasValidationErrorForTitle()
    {
        var command = new CreateQuizSetCommand(new string('a', 201), "Description");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithTitleAtMaxLength_HasNoValidationErrorForTitle()
    {
        // Arrange: exactly 200 characters — the boundary itself must be valid
        var command = new CreateQuizSetCommand(new string('a', 200), "Description");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithEmptyDescription_HasValidationErrorForDescription()
    {
        var command = new CreateQuizSetCommand("Title", "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithDescriptionExceedingMaxLength_HasValidationErrorForDescription()
    {
        var command = new CreateQuizSetCommand("Title", new string('a', 2001));

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = new CreateQuizSetCommand("Title", "Description");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UpdateQuizSetDetailsCommandValidatorTests
{
    private readonly UpdateQuizSetDetailsCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyTitle_HasValidationErrorForTitle()
    {
        var command = new UpdateQuizSetDetailsCommand(Guid.CreateVersion7(), "", "Description");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = new UpdateQuizSetDetailsCommand(Guid.CreateVersion7(), "New title", "New description");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // Note: this validator reuses the exact same QuizSetTitle()/QuizSetDescription() extension
    // rules as CreateQuizSetCommandValidator, so we don't repeat every boundary case here —
    // that would just be re-testing the shared extension method twice.
}

public class AddQuestionCommandValidatorTests
{
    private readonly AddQuestionCommandValidator _validator = new();

    private static AddQuestionCommand CreateValidCommand() => new(
        Guid.CreateVersion7(),
        Text: "2 + 2 = ?",
        QuestionType: QuestionType.SingleChoice,
        TimeLimitSeconds: 30,
        Points: 100,
        Options: [new AnswerOptionDto("4", true, 0), new AnswerOptionDto("5", false, 1)]);

    [Fact]
    public void Validate_WithEmptyText_HasValidationErrorForText()
    {
        var command = CreateValidCommand() with { Text = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void Validate_WithTextExceedingMaxLength_HasValidationErrorForText()
    {
        var command = CreateValidCommand() with { Text = new string('a', 501) };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveTimeLimit_HasValidationErrorForTimeLimitSeconds(int timeLimit)
    {
        var command = CreateValidCommand() with { TimeLimitSeconds = timeLimit };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TimeLimitSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_WithNonPositivePoints_HasValidationErrorForPoints(int points)
    {
        var command = CreateValidCommand() with { Points = points };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Points);
    }

    [Fact]
    public void Validate_WithNoOptions_HasValidationErrorForOptions()
    {
        var command = CreateValidCommand() with { Options = [] };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Options);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = CreateValidCommand();

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
