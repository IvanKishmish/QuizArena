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

    // AddQuestion/RemoveQuestion and the Questions collection were removed from QuizSet (see S3 in the
    // review response): every real code path already managed questions through IQuestionStore/Question
    // directly, so those members only ever existed in this test file. Question lifecycle is now covered by
    // QuestionsHandlerTests and QuestionTests instead.

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
}
