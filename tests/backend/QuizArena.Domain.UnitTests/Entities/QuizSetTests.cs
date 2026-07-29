using ErrorOr;
using FluentAssertions;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class QuizSetTests
{
    [Fact]
    public void Create_WithValidArgs_ReturnsSuccess()
    {
        //Arrange
        var args = ValidArgs();
 
        //Act
        ErrorOr<QuizSet> result = QuizSet.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.OwnerId.Should().Be(args.OwnerId);
        result.Value.Title.Should().Be(args.Title);
        result.Value.Description.Should().Be(args.Description);
        result.Value.Visibility.Should().Be(Visibility.Private);
        result.Value.Questions.Should().BeEmpty();
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidTitle_ReturnsValidationError(string? title)
    {
        //Arrange
        var args = ValidArgs() with { Title = title! };
 
        //Act
        var result = QuizSet.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "QuizSet.TitleRequired");
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidDescription_ReturnsValidationError(string? description)
    {
        //Arrange
        var args = ValidArgs() with { Description = description! };
 
        //Act
        var result = QuizSet.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "QuizSet.DescriptionRequired");
    }
 
    [Fact]
    public void AddQuestion_WithValidArgs_AddsQuestionToList()
    {
        //Arrange
        var quizSet = CreateQuizSet();
 
        //Act
        var result = quizSet.AddQuestion(ValidQuestionArgs());
 
        //Assert
        result.IsError.Should().BeFalse();
        quizSet.Questions.Should().HaveCount(1);
    }
 
    [Fact]
    public void AddQuestion_WithInvalidArgs_ReturnsErrorAndDoesNotAddQuestion()
    {
        //Arrange
        var quizSet = CreateQuizSet();
        var invalidArgs = ValidQuestionArgs() with { Text = "" };
 
        //Act
        var result = quizSet.AddQuestion(invalidArgs);
 
        //Assert
        result.IsError.Should().BeTrue();
        quizSet.Questions.Should().BeEmpty();
    }
 
    [Fact]
    public void RemoveQuestion_WhenQuestionExists_RemovesIt()
    {
        //Arrange
        var quizSet = CreateQuizSet();
        quizSet.AddQuestion(ValidQuestionArgs());
        var questionId = quizSet.Questions[0].Id;
 
        //Act
        var result = quizSet.RemoveQuestion(questionId);
 
        //Assert
        result.IsError.Should().BeFalse();
        quizSet.Questions.Should().BeEmpty();
    }
 
    [Fact]
    public void RemoveQuestion_WhenQuestionDoesNotExist_ReturnsNotFoundError()
    {
        //Arrange
        var quizSet = CreateQuizSet();
 
        //Act
        var result = quizSet.RemoveQuestion(Guid.CreateVersion7());
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("QuizSet.QuestionNotFound");
    }
 
    [Fact]
    public void UpdateDetails_WithValidValues_UpdatesTitleAndDescription()
    {
        //Arrange
        var quizSet = CreateQuizSet();
 
        //Act
        var result = quizSet.UpdateDetails("New Title", "New Description");
 
        //Assert
        result.IsError.Should().BeFalse();
        quizSet.Title.Should().Be("New Title");
        quizSet.Description.Should().Be("New Description");
    }
 
    [Theory]
    [InlineData(null, "Description")]
    [InlineData("", "Description")]
    [InlineData("Title", null)]
    [InlineData("Title", "")]
    public void UpdateDetails_WithInvalidValues_ReturnsValidationErrorAndKeepsOldValues(string? title, string? description)
    {
        //Arrange
        var quizSet = CreateQuizSet();
 
        //Act
        var result = quizSet.UpdateDetails(title!, description!);
 
        //Assert
        result.IsError.Should().BeTrue();
        quizSet.Title.Should().Be("Quiz");
        quizSet.Description.Should().Be("A quiz set");
    }
 
    [Fact]
    public void Publish_SetsVisibilityToPublic()
    {
        //Arrange
        var quizSet = CreateQuizSet();
 
        //Act
        var result = quizSet.Publish();
 
        //Assert
        result.IsError.Should().BeFalse();
        quizSet.Visibility.Should().Be(Visibility.Public);
    }
 
    [Fact]
    public void Unpublish_SetsVisibilityToPrivate()
    {
        //Arrange
        var quizSet = CreateQuizSet();
        quizSet.Publish();
 
        //Act
        var result = quizSet.Unpublish();
 
        //Assert
        result.IsError.Should().BeFalse();
        quizSet.Visibility.Should().Be(Visibility.Private);
    }
 
    private static QuizSet CreateQuizSet() =>
        QuizSet.Create(ValidArgs()).Value;
 
    private static QuizSetCreationParams ValidArgs() =>
        new(Guid.CreateVersion7(), "Quiz", "A quiz set");
 
    private static QuestionCreationParams ValidQuestionArgs() =>
        new(
            "What is 2+2?",
            QuestionType.SingleChoice,
            30,
            100,
            [
                new AnswerOptionParams("3", false, 0),
                new AnswerOptionParams("4", true, 1)
            ]);
}