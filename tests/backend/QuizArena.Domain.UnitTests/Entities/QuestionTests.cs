using FluentAssertions;
using ErrorOr;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class QuestionTests
{
    #region Create
 
    [Fact]
    public void Create_WithValidSingleChoiceArgs_ReturnsSuccess()
    {
        //Arrange
        var args = SingleChoiceArgs();
 
        //Act
        ErrorOr<Question> result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.Text.Should().Be(args.Text);
        result.Value.QuestionType.Should().Be(QuestionType.SingleChoice);
        result.Value.Options.Should().HaveCount(2);
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidText_ReturnsValidationError(string? text)
    {
        //Arrange
        var args = SingleChoiceArgs() with { Text = text! };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.TextRequired");
    }
 
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithInvalidTimeLimit_ReturnsValidationError(int timeLimit)
    {
        //Arrange
        var args = SingleChoiceArgs() with { TimeLimitSeconds = timeLimit };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.InvalidTimeLimit");
    }
 
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidPoints_ReturnsValidationError(int points)
    {
        //Arrange
        var args = SingleChoiceArgs() with { Points = points };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.InvalidPoints");
    }
 
    [Fact]
    public void Create_WithNoOptions_ReturnsOptionsRequiredErrorOnly()
    {
        //Arrange
        var args = SingleChoiceArgs() with { Options = [] };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "Question.OptionsRequired");
    }
 
    [Fact]
    public void Create_SingleChoiceWithNoCorrectOption_ReturnsValidationError()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            Options =
            [
                new AnswerOptionParams("A", false, 0),
                new AnswerOptionParams("B", false, 1)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.SingleCorrectAnswerRequired");
    }
 
    [Fact]
    public void Create_SingleChoiceWithMultipleCorrectOptions_ReturnsValidationError()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            Options =
            [
                new AnswerOptionParams("A", true, 0),
                new AnswerOptionParams("B", true, 1)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.SingleCorrectAnswerRequired");
    }
 
    [Fact]
    public void Create_TrueFalseWithExactlyOneCorrectOption_ReturnsSuccess()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            QuestionType = QuestionType.TrueFalse,
            Options =
            [
                new AnswerOptionParams("True", true, 0),
                new AnswerOptionParams("False", false, 1)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
    }
 
    [Fact]
    public void Create_MultipleChoiceWithNoCorrectOptions_ReturnsValidationError()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            QuestionType = QuestionType.MultipleChoice,
            Options =
            [
                new AnswerOptionParams("A", false, 0),
                new AnswerOptionParams("B", false, 1)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.AtLeastOneCorrectAnswerRequired");
    }
 
    [Fact]
    public void Create_MultipleChoiceWithAtLeastOneCorrectOption_ReturnsSuccess()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            QuestionType = QuestionType.MultipleChoice,
            Options =
            [
                new AnswerOptionParams("A", true, 0),
                new AnswerOptionParams("B", true, 1),
                new AnswerOptionParams("C", false, 2)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
    }
 
    [Fact]
    public void Create_OrderingWithDuplicateOrderIndices_ReturnsValidationError()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            QuestionType = QuestionType.Ordering,
            Options =
            [
                new AnswerOptionParams("A", false, 0),
                new AnswerOptionParams("B", false, 0)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Question.DuplicateOrderIndex");
    }
 
    [Fact]
    public void Create_OrderingWithUniqueOrderIndices_ReturnsSuccess()
    {
        //Arrange
        var args = SingleChoiceArgs() with
        {
            QuestionType = QuestionType.Ordering,
            Options =
            [
                new AnswerOptionParams("A", false, 0),
                new AnswerOptionParams("B", false, 1)
            ]
        };
 
        //Act
        var result = Question.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
    }
 
    #endregion
 
    #region CalculateScore - invalid answer format
 
    [Fact]
    public void CalculateScore_WithEmptySelection_ReturnsZero()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void CalculateScore_WithOutOfRangeIndex_ReturnsZero(int index)
    {
        //Arrange
        var question = CreateSingleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([index], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_WithDuplicateIndices_ReturnsZero()
    {
        //Arrange
        var question = CreateMultipleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([0, 0], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_OrderingWithFewerIndicesThanOptions_ReturnsZero()
    {
        //Arrange
        var question = CreateOrderingQuestion();
 
        //Act
        var score = question.CalculateScore([0], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    #endregion
 
    #region CalculateScore - correctness per question type
 
    [Fact]
    public void CalculateScore_SingleChoice_WithCorrectIndex_ReturnsPositiveScore()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion(); // correct option at index 1
 
        //Act
        var score = question.CalculateScore([1], 0);
 
        //Assert
        score.Should().BeGreaterThan(0);
    }
 
    [Fact]
    public void CalculateScore_SingleChoice_WithWrongIndex_ReturnsZero()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([0], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_SingleChoice_WithMultipleSelectedIndices_ReturnsZero()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([0, 1], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_MultipleChoice_WithExactCorrectSet_ReturnsPositiveScore()
    {
        //Arrange
        var question = CreateMultipleChoiceQuestion(); // correct: indices 0 and 2
 
        //Act
        var score = question.CalculateScore([0, 2], 0);
 
        //Assert
        score.Should().BeGreaterThan(0);
    }
 
    [Fact]
    public void CalculateScore_MultipleChoice_WithPartialCorrectSet_ReturnsZero()
    {
        //Arrange
        var question = CreateMultipleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([0], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_MultipleChoice_WithExtraIncorrectIndex_ReturnsZero()
    {
        //Arrange
        var question = CreateMultipleChoiceQuestion();
 
        //Act
        var score = question.CalculateScore([0, 1, 2], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    [Fact]
    public void CalculateScore_Ordering_WithCorrectSequence_ReturnsPositiveScore()
    {
        //Arrange
        var question = CreateOrderingQuestion();
 
        //Act
        var score = question.CalculateScore([0, 1, 2], 0);
 
        //Assert
        score.Should().BeGreaterThan(0);
    }
 
    [Fact]
    public void CalculateScore_Ordering_WithWrongSequence_ReturnsZero()
    {
        //Arrange
        var question = CreateOrderingQuestion();
 
        //Act
        var score = question.CalculateScore([2, 1, 0], 0);
 
        //Assert
        score.Should().Be(0);
    }
 
    #endregion
 
    #region CalculateScore - time bonus
 
    [Fact]
    public void CalculateScore_WithZeroElapsedTime_ReturnsFullPoints()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion(points: 100, timeLimitSeconds: 10);
 
        //Act
        var score = question.CalculateScore([1], 0);
 
        //Assert
        score.Should().Be(100);
    }
 
    [Fact]
    public void CalculateScore_WithElapsedTimeEqualToLimit_ReturnsHalfPoints()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion(points: 100, timeLimitSeconds: 10);
 
        //Act
        var score = question.CalculateScore([1], 10);
 
        //Assert
        score.Should().Be(50);
    }
 
    [Fact]
    public void CalculateScore_WithElapsedTimeExceedingLimit_ClampsToHalfPoints()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion(points: 100, timeLimitSeconds: 10);
 
        //Act
        var score = question.CalculateScore([1], 15);
 
        //Assert
        score.Should().Be(50);
    }
 
    [Fact]
    public void CalculateScore_WithHalfElapsedTime_ReturnsThreeQuarterPoints()
    {
        //Arrange
        var question = CreateSingleChoiceQuestion(points: 100, timeLimitSeconds: 10);
 
        //Act
        var score = question.CalculateScore([1], 5);
 
        //Assert
        score.Should().Be(75);
    }
 
    #endregion
 
    private static Question CreateSingleChoiceQuestion(int points = 100, int timeLimitSeconds = 30) =>
        Question.Create(SingleChoiceArgs() with { Points = points, TimeLimitSeconds = timeLimitSeconds }).Value;
 
    private static Question CreateMultipleChoiceQuestion() =>
        Question.Create(SingleChoiceArgs() with
        {
            QuestionType = QuestionType.MultipleChoice,
            Options =
            [
                new AnswerOptionParams("A", true, 0),
                new AnswerOptionParams("B", false, 1),
                new AnswerOptionParams("C", true, 2)
            ]
        }).Value;
 
    private static Question CreateOrderingQuestion() =>
        Question.Create(SingleChoiceArgs() with
        {
            QuestionType = QuestionType.Ordering,
            Options =
            [
                new AnswerOptionParams("First", false, 0),
                new AnswerOptionParams("Second", false, 1),
                new AnswerOptionParams("Third", false, 2)
            ]
        }).Value;
 
    private static QuestionCreationParams SingleChoiceArgs() =>
        new(
            "What is the capital of France?",
            QuestionType.SingleChoice,
            30,
            100,
            [
                new AnswerOptionParams("London", false, 0),
                new AnswerOptionParams("Paris", true, 1)
            ]);
}