using FluentAssertions;
using ErrorOr;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class GameHistoryEntryTests
{
    [Fact]
    public void Create_WithValidArgs_ReturnsSuccess()
    {
        //Arrange
        var args = ValidArgs();
 
        //Act
        ErrorOr<GameHistoryEntry> result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.QuizSetId.Should().Be(args.QuizSetId);
        result.Value.ParticipantUserId.Should().Be(args.ParticipantUserId);
        result.Value.DisplayName.Should().Be(args.DisplayName);
        result.Value.FinalScore.Should().Be(args.FinalScore);
        result.Value.Placement.Should().Be(args.Placement);
    }
 
    [Fact]
    public void Create_WithNullParticipantUserId_ReturnsSuccess()
    {
        //Arrange
        var args = ValidArgs() with { ParticipantUserId = null };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.ParticipantUserId.Should().BeNull();
    }
 
    [Fact]
    public void Create_WithEmptyQuizSetId_ReturnsValidationError()
    {
        //Arrange
        var args = ValidArgs() with { QuizSetId = Guid.Empty };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameHistoryEntry.QuizSetIdRequired");
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidDisplayName_ReturnsValidationError(string? displayName)
    {
        //Arrange
        var args = ValidArgs() with { DisplayName = displayName! };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameHistoryEntry.DisplayNameRequired");
    }
 
    [Fact]
    public void Create_WithNegativeFinalScore_ReturnsValidationError()
    {
        //Arrange
        var args = ValidArgs() with { FinalScore = -1 };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameHistoryEntry.InvalidScore");
    }
 
    [Fact]
    public void Create_WithZeroFinalScore_ReturnsSuccess()
    {
        //Arrange
        var args = ValidArgs() with { FinalScore = 0 };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
    }
 
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidPlacement_ReturnsValidationError(int placement)
    {
        //Arrange
        var args = ValidArgs() with { Placement = placement };
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameHistoryEntry.InvalidPlacement");
    }
 
    [Fact]
    public void Create_WithMultipleInvalidFields_ReturnsAllValidationErrors()
    {
        //Arrange
        var args = new GameHistoryEntryCreationParams(Guid.Empty, null, "", -1, 0);
 
        //Act
        var result = GameHistoryEntry.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(4);
    }
 
    private static GameHistoryEntryCreationParams ValidArgs() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ivan", 100, 1);
}