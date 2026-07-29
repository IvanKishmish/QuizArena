using FluentAssertions;
using ErrorOr;
using QuizArena.Domain.Entities;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class PlayerTests
{
    [Fact]
    public void Create_WithValidNickName_ReturnsSuccess()
    {
        //Arrange
        var id = Guid.CreateVersion7();
 
        //Act
        ErrorOr<Player> result = Player.Create(id, "Vanya");
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.NickName.Should().Be("Vanya");
        result.Value.TotalGamesPlayed.Should().Be(0);
        result.Value.TotalScore.Should().Be(0);
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abcd")] // 4 chars, less than minimum of 5
    public void Create_WithInvalidNickName_ReturnsValidationError(string? nickName)
    {
        //Act
        var result = Player.Create(Guid.CreateVersion7(), nickName!);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Player.InvalidNickName");
    }
 
    [Fact]
    public void Create_WithNickNameOfMinimumLength_ReturnsSuccess()
    {
        //Act
        var result = Player.Create(Guid.CreateVersion7(), "abcde"); // exactly 5 chars
 
        //Assert
        result.IsError.Should().BeFalse();
    }
 
    [Fact]
    public void UpdateNickName_WithValidValue_UpdatesNickName()
    {
        //Arrange
        var player = CreatePlayer();
 
        //Act
        var result = player.UpdateNickName("NewNick");
 
        //Assert
        result.IsError.Should().BeFalse();
        player.NickName.Should().Be("NewNick");
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcd")]
    public void UpdateNickName_WithInvalidValue_ReturnsValidationErrorAndKeepsOldName(string? newNickName)
    {
        //Arrange
        var player = CreatePlayer();
 
        //Act
        var result = player.UpdateNickName(newNickName!);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Player.InvalidNickName");
        player.NickName.Should().Be("Vanya");
    }
 
    [Fact]
    public void RecordGameResult_WithPositiveScore_IncrementsGamesAndAddsScore()
    {
        //Arrange
        var player = CreatePlayer();
 
        //Act
        var result = player.RecordGameResult(50);
 
        //Assert
        result.IsError.Should().BeFalse();
        player.TotalGamesPlayed.Should().Be(1);
        player.TotalScore.Should().Be(50);
    }
 
    [Fact]
    public void RecordGameResult_CalledMultipleTimes_AccumulatesStats()
    {
        //Arrange
        var player = CreatePlayer();
 
        //Act
        player.RecordGameResult(30);
        player.RecordGameResult(20);
 
        //Assert
        player.TotalGamesPlayed.Should().Be(2);
        player.TotalScore.Should().Be(50);
    }
 
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void RecordGameResult_WithZeroOrNegativeScore_ReturnsValidationError(int score)
    {
        //Arrange
        var player = CreatePlayer();
 
        //Act
        var result = player.RecordGameResult(score);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Player.NegativeOrEqualToZeroScore");
        player.TotalGamesPlayed.Should().Be(0);
        player.TotalScore.Should().Be(0);
    }
 
    private static Player CreatePlayer() =>
        Player.Create(Guid.CreateVersion7(), "Vanya").Value;
}