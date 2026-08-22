using FluentAssertions;
using ErrorOr;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.UnitTests.Entities;

public sealed class GameRoomTests
{
    #region Create
 
    [Fact]
    public void Create_WithValidArgs_ReturnsSuccess()
    {
        //Arrange
        var args = ValidArgs();
 
        //Act
        ErrorOr<GameRoom> result = GameRoom.Create(args);
 
        //Assert
        result.IsError.Should().BeFalse();
        result.Value.RoomCode.Should().Be(args.RoomCode);
        result.Value.QuizSetId.Should().Be(args.QuizSetId);
        result.Value.HostId.Should().Be(args.HostId);
        result.Value.Status.Should().Be(GameRoomStatus.Waiting);
        result.Value.Participants.Should().BeEmpty();
    }
 
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithInvalidRoomCode_ReturnsValidationError(string? roomCode)
    {
        //Arrange
        var args = ValidArgs() with { RoomCode = roomCode! };
 
        //Act
        var result = GameRoom.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameRoom.RoomCodeRequired");
    }
 
    [Fact]
    public void Create_WithEmptyQuizSetId_ReturnsValidationError()
    {
        //Arrange
        var args = ValidArgs() with { QuizSetId = Guid.Empty };
 
        //Act
        var result = GameRoom.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameRoom.QuizSetIdRequired");
    }
 
    [Fact]
    public void Create_WithEmptyHostId_ReturnsValidationError()
    {
        //Arrange
        var args = ValidArgs() with { HostId = Guid.Empty };
 
        //Act
        var result = GameRoom.Create(args);
 
        //Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "GameRoom.HostIdRequired");
    }
 
    #endregion
 
    #region AddParticipant
 
    [Fact]
    public void AddParticipant_WithValidUser_AddsParticipant()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
 
        //Assert
        result.IsError.Should().BeFalse();
        room.Participants.Should().HaveCount(1);
    }
 
    [Fact]
    public void AddParticipant_WithValidGuest_AddsParticipant()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.AddParticipant(null, Guid.CreateVersion7(), "Guest123");
 
        //Assert
        result.IsError.Should().BeFalse();
        room.Participants.Should().HaveCount(1);
    }
 
    [Fact]
    public void AddParticipant_WhenSameUserJoinsTwice_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        var userId = Guid.CreateVersion7();
        room.AddParticipant(userId, null, "Ivan");
 
        //Act
        var result = room.AddParticipant(userId, null, "Ivan Again");
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.AlreadyJoined");
        room.Participants.Should().HaveCount(1);
    }
 
    [Fact]
    public void AddParticipant_WhenSameGuestJoinsTwice_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        var guestId = Guid.CreateVersion7();
        room.AddParticipant(null, guestId, "Guest");
 
        //Act
        var result = room.AddParticipant(null, guestId, "Guest Again");
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.AlreadyJoined");
    }
 
    [Fact]
    public void AddParticipant_WithInvalidDisplayName_PropagatesParticipantError()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.AddParticipant(Guid.CreateVersion7(), null, "");
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.DisplayNameRequired");
        room.Participants.Should().BeEmpty();
    }
 
    [Fact]
    public void AddParticipant_WhenRoomAlreadyStarted_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
 
        //Act
        var result = room.AddParticipant(Guid.CreateVersion7(), null, "Late Player");
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotAcceptingParticipants");
    }
 
    [Fact]
    public void AddParticipant_WhenRoomFinished_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        room.Finish();
 
        //Act
        var result = room.AddParticipant(Guid.CreateVersion7(), null, "Late Player");
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotAcceptingParticipants");
    }
 
    #endregion
 
    #region Start
 
    [Fact]
    public void Start_WithAtLeastOneParticipant_TransitionsToInProgress()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
 
        //Act
        var result = room.Start();
 
        //Assert
        result.IsError.Should().BeFalse();
        room.Status.Should().Be(GameRoomStatus.InProgress);
        // S11: Start() leaves CurrentQuestionIndex at -1 ("no question active yet") instead of 0 — advancing
        // to question #0 is now solely NextQuestion()'s job, same as every other question. See
        // NextQuestion_WhenInProgress_IncrementsQuestionIndex below.
        room.CurrentQuestionIndex.Should().Be(-1);
        room.StartedAt.Should().NotBeNull();
    }
 
    [Fact]
    public void Start_GrantsDefaultPowerUpsToAllParticipants()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
 
        //Act
        room.Start();
 
        //Assert
        // S8: the starting kit is now an explicit list on Participant, not "whatever's in the enum" — a
        // future PowerUpType shouldn't get handed to every player for free just because it was added.
        room.Participants[0].AvailablePowerUps.Should().BeEquivalentTo(
        [
            PowerUpType.Freeze,
            PowerUpType.FiftyFifty,
            PowerUpType.DoubleOrNothing
        ]);
    }
 
    [Fact]
    public void Start_WithNoParticipants_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.Start();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotEnoughParticipants");
        room.Status.Should().Be(GameRoomStatus.Waiting);
    }
 
    [Fact]
    public void Start_WhenAlreadyInProgress_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
 
        //Act
        var result = room.Start();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.InvalidStateForStart");
    }
 
    [Fact]
    public void Start_WhenAlreadyFinished_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        room.Finish();
 
        //Act
        var result = room.Start();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.InvalidStateForStart");
    }
 
    #endregion
 
    #region NextQuestion
 
    [Fact]
    public void NextQuestion_WhenInProgress_IncrementsQuestionIndex()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
 
        //Act
        var result = room.NextQuestion();
 
        //Assert
        result.IsError.Should().BeFalse();
        // S11: first NextQuestion() call after Start() moves -1 -> 0 (revealing question #0) — it used to
        // move 0 -> 1, so question #0 was never sent to anyone.
        room.CurrentQuestionIndex.Should().Be(0);
        room.CurrentQuestionStartedAt.Should().NotBeNull();
    }
 
    [Fact]
    public void NextQuestion_WhenNotStarted_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.NextQuestion();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }
 
    [Fact]
    public void NextQuestion_WhenFinished_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        room.Finish();
 
        //Act
        var result = room.NextQuestion();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }
 
    [Fact]
    public void NextQuestion_CalledMultipleTimes_KeepsIncrementing()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
 
        //Act
        room.NextQuestion();
        room.NextQuestion();
 
        //Assert
        // S11: starts at -1, so two calls land on 0 then 1 (question #0, then #1) — not 1 then 2.
        room.CurrentQuestionIndex.Should().Be(1);
    }
 
    #endregion
 
    #region Finish
 
    [Fact]
    public void Finish_WhenInProgress_TransitionsToFinished()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
 
        //Act
        var result = room.Finish();
 
        //Assert
        result.IsError.Should().BeFalse();
        room.Status.Should().Be(GameRoomStatus.Finished);
        room.FinishedAt.Should().NotBeNull();
    }
 
    [Fact]
    public void Finish_WhenNotStarted_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
 
        //Act
        var result = room.Finish();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }
 
    [Fact]
    public void Finish_WhenAlreadyFinished_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        room.Finish();
 
        //Act
        var result = room.Finish();
 
        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }
 
    #endregion
 
    #region K3/K4 plumbing

    [Fact]
    public void AddParticipant_WhenRoomIsFull_ReturnsValidationError()
    {
        //Arrange
        var room = CreateRoom();

        for (var i = 0; i < GameRoom.MaxParticipants; i++)
            room.AddParticipant(Guid.CreateVersion7(), null, $"Player {i}");

        //Act
        var result = room.AddParticipant(Guid.CreateVersion7(), null, "One Too Many");

        //Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.RoomFull");
        room.Participants.Should().HaveCount(GameRoom.MaxParticipants);
    }

    [Fact]
    public void Create_NewRoom_HasVersionZero()
    {
        // Version 0 is what IGameRoomStore.SaveAsync treats as "this room must not already exist" — see
        // GameRoomStore's compare-and-swap script.
        CreateRoom().Version.Should().Be(0);
    }

    #endregion

    private static GameRoom CreateRoom() =>
        GameRoom.Create(ValidArgs()).Value;
 
    private static GameRoomCreationParams ValidArgs() =>
        new("ABCD12", Guid.CreateVersion7(), Guid.CreateVersion7());
}