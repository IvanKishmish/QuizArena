using ErrorOr;
using FluentAssertions;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class ParticipantTests
{
    [Fact]
    public void Create_WithValidUserId_ReturnsSuccess()
    {
        //Arrange
        var userId = Guid.CreateVersion7();
        var displayName = "Ivan";
        
        //Act
        ErrorOr<Participant> result = Participant.Create(userId, null, displayName);
        
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.DisplayName.Should().Be(displayName);
        result.Value.UserId.Should().Be(userId);
        result.Value.GuestId.Should().BeNull();
        result.Value.Score.Should().Be(0);
    }

    [Fact]
    public void Create_WithValidGuestId_ReturnsSuccess()
    {
        //Arrange
        var guestId = Guid.CreateVersion7();
        
        //Act
        var result = Participant.Create(null, guestId, "Guest123");
        
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.GuestId.Should().Be(guestId);
        result.Value.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidDisplayName_ReturnsValidationError(string? displayName)
    {
        //Arrange
        var userId = Guid.CreateVersion7();
        
        //Act
        var result = Participant.Create(userId, null, displayName!);
        
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.DisplayNameRequired");
    }

    [Fact]
    public void Create_WithoutUserIdOrGuestId_ReturnsValidationError()
    {
        //Act
        var result = Participant.Create(null, null, "Ivan");
        
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.IdentityRequired");
    }

    [Fact]
    public void Create_WithBothUserIdAndGuestId_ReturnsValidationError()
    {
        //Act
        var result = Participant.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ivan");
        
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.ConflictingIdentity");
    }

    [Fact]
    public void AddScore_WithPositivePoints_IncreasesScore()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.AddScore(10);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(10);
    }
    
    [Fact]
    public void AddScore_WithNegativePoints_ReturnsValidationError()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.AddScore(-5);

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NegativeScore");
        participant.Score.Should().Be(0);
    }
    
    [Fact]
    public void AddScore_WithZeroPoints_IsAllowedAndDoesNotChangeScore()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.AddScore(0);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(0);
    }

    [Fact]
    public void AddScore_WithActiveDoubleOrNothing_DoublesPointsAndClearsIt()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();
        participant.UsePowerUp(PowerUpType.DoubleOrNothing);

        //Act
        var result = participant.AddScore(10);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(20);
        participant.ActiveDoubleOrNothing.Should().BeNull();
    }
    
    [Fact]
    public void GrantDefaultPowerUps_FillsAllPowerUpTypes()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        participant.GrantDefaultPowerUps();

        //Assert
        participant.AvailablePowerUps.Should().BeEquivalentTo(Enum.GetValues<PowerUpType>());
    }

    [Fact]
    public void GrantDefaultPowerUps_WhenCalledAgain_ResetsPreviouslyUsedPowerUps()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();
        participant.UsePowerUp(PowerUpType.Freeze);

        //Act
        participant.GrantDefaultPowerUps();

        //Assert
        participant.AvailablePowerUps.Should().Contain(PowerUpType.Freeze);
    }
    
    [Fact]
    public void UsePowerUp_WhenAvailable_RemovesItFromAvailableList()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();

        //Act
        var result = participant.UsePowerUp(PowerUpType.FiftyFifty);

        //Assert
        result.IsError.Should().BeFalse();
        participant.AvailablePowerUps.Should().NotContain(PowerUpType.FiftyFifty);
    }

    [Fact]
    public void UsePowerUp_WhenNotAvailable_ReturnsValidationError()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.UsePowerUp(PowerUpType.Freeze);

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.PowerUpNotAvailable");
    }

    [Fact]
    public void UsePowerUp_WhenAlreadyUsed_ReturnsValidationError()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();
        participant.UsePowerUp(PowerUpType.Freeze);

        //Act
        var result = participant.UsePowerUp(PowerUpType.Freeze);

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.PowerUpNotAvailable");
    }

    [Fact]
    public void UsePowerUp_DoubleOrNothing_SetsActiveDoubleOrNothing()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();

        //Act
        var result = participant.UsePowerUp(PowerUpType.DoubleOrNothing);

        //Assert
        result.IsError.Should().BeFalse();
        participant.ActiveDoubleOrNothing.Should().Be(PowerUpType.DoubleOrNothing);
    }

    [Fact]
    public void UsePowerUp_NonDoubleOrNothing_DoesNotSetActiveDoubleOrNothing()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();

        //Act
        participant.UsePowerUp(PowerUpType.Freeze);

        //Assert
        participant.ActiveDoubleOrNothing.Should().BeNull();
    }

    [Fact]
    public void ApplyFreeze_SetsIsFrozenAndFrozenUntilInTheFuture()
    {
        //Arrange
        var participant = CreateParticipant();
        var before = DateTimeOffset.UtcNow;

        //Act
        participant.ApplyFreeze(TimeSpan.FromSeconds(30));

        //Assert
        participant.IsFrozen.Should().BeTrue();
        participant.FrozenUntil.Should().NotBeNull();
        participant.FrozenUntil.Should().BeOnOrAfter(before.AddSeconds(30));
    }

    [Fact]
    public void ClearFreezeIfExpired_WhenFreezeNotExpired_KeepsParticipantFrozen()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.ApplyFreeze(TimeSpan.FromMinutes(5));

        //Act
        participant.ClearFreezeIfExpired();

        //Assert
        participant.IsFrozen.Should().BeTrue();
        participant.FrozenUntil.Should().NotBeNull();
    }

    [Fact]
    public void ClearFreezeIfExpired_WhenFreezeExpired_ClearsFreezeState()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.ApplyFreeze(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(20);

        //Act
        participant.ClearFreezeIfExpired();

        //Assert
        participant.IsFrozen.Should().BeFalse();
        participant.FrozenUntil.Should().BeNull();
    }

    [Fact]
    public void ClearFreezeIfExpired_WhenNeverFrozen_DoesNothing()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        participant.ClearFreezeIfExpired();

        //Assert
        participant.IsFrozen.Should().BeFalse();
        participant.FrozenUntil.Should().BeNull();
    }
    
    private static Participant CreateParticipant()
        => Participant.Create(Guid.CreateVersion7(), null, "Ivan").Value;
}