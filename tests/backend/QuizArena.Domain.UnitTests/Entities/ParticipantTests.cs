using ErrorOr;
using FluentAssertions;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Enums;
using QuizArena.Domain.UnitTests.Common;

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

    // K2: AddScore(points) was replaced with SubmitAnswer(questionId, points) — the extra questionId is
    // what makes a second submission for the same question rejected instead of scored again.

    [Fact]
    public void SubmitAnswer_WithPositivePoints_IncreasesScore()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.SubmitAnswer(Guid.CreateVersion7(), 10);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(10);
    }
    
    [Fact]
    public void SubmitAnswer_WithNegativePoints_ReturnsValidationError()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.SubmitAnswer(Guid.CreateVersion7(), -5);

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NegativeScore");
        participant.Score.Should().Be(0);
    }
    
    [Fact]
    public void SubmitAnswer_WithZeroPoints_IsAllowedAndDoesNotChangeScore()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        var result = participant.SubmitAnswer(Guid.CreateVersion7(), 0);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(0);
    }

    [Fact]
    public void SubmitAnswer_WithActiveDoubleOrNothing_DoublesPointsAndClearsIt()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.GrantDefaultPowerUps();
        participant.UsePowerUp(PowerUpType.DoubleOrNothing);

        //Act
        var result = participant.SubmitAnswer(Guid.CreateVersion7(), 10);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(20);
        participant.ActiveDoubleOrNothing.Should().BeNull();
    }

    [Fact]
    public void SubmitAnswer_ForTheSameQuestionTwice_RejectsTheSecondSubmission()
    {
        //Arrange
        var participant = CreateParticipant();
        var questionId = Guid.CreateVersion7();
        participant.SubmitAnswer(questionId, 10);

        //Act
        var result = participant.SubmitAnswer(questionId, 10);

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.AlreadyAnswered");
        // The score from the rejected replay must not be added again.
        participant.Score.Should().Be(10);
    }

    [Fact]
    public void SubmitAnswer_ForADifferentQuestion_IsAccepted()
    {
        //Arrange
        var participant = CreateParticipant();
        participant.SubmitAnswer(Guid.CreateVersion7(), 10);

        //Act
        var result = participant.SubmitAnswer(Guid.CreateVersion7(), 5);

        //Assert
        result.IsError.Should().BeFalse();
        participant.Score.Should().Be(15);
    }

    [Fact]
    public void SubmitAnswer_RecordsTheQuestionInAnsweredQuestionIds()
    {
        //Arrange
        var participant = CreateParticipant();
        var questionId = Guid.CreateVersion7();

        //Act
        participant.SubmitAnswer(questionId, 10);

        //Assert
        participant.AnsweredQuestionIds.Should().ContainSingle().Which.Should().Be(questionId);
    }
    
    [Fact]
    public void GrantDefaultPowerUps_FillsAllPowerUpTypes()
    {
        //Arrange
        var participant = CreateParticipant();

        //Act
        participant.GrantDefaultPowerUps();

        //Assert
        // S8: explicit kit, not "every enum value" — see GameRoomTests' equivalent assertion for why.
        participant.AvailablePowerUps.Should().BeEquivalentTo(
        [
            PowerUpType.Freeze,
            PowerUpType.FiftyFifty,
            PowerUpType.DoubleOrNothing
        ]);
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
        // S13: used to be `ApplyFreeze(1ms); Thread.Sleep(20);` — a real wall-clock wait to make "expired"
        // true, and exactly the kind of assumption that occasionally flakes under CI load. A fixed
        // TimeProvider representing "an hour ago" makes FrozenUntil already-expired deterministically,
        // with no timing dependency at all.
        //Arrange
        var participant = CreateParticipant();
        var anHourAgo = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
        participant.ApplyFreeze(TimeSpan.Zero, anHourAgo);

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