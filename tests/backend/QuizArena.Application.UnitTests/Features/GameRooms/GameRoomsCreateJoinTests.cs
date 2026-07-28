using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.GameRooms.Commands.CreateGameRoom;
using QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Application.UnitTests.Features.GameRooms;

public class CreateGameRoomCommandHandlerTests : QuizArenaHandlerTestBase
{
    private readonly Mock<IRoomCodeGenerator> _roomCodeGeneratorMock = new();
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();

    private CreateGameRoomCommandHandler CreateHandler()
        => new(DbContext, CurrentUserMock.Object, _roomCodeGeneratorMock.Object, _gameRoomStoreMock.Object);

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        // Arrange
        CurrentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new CreateGameRoomCommand(Guid.CreateVersion7()));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotQuizSetOwner_ReturnsForbiddenError()
    {
        // Arrange: quiz set is owned by someone else
        var quizSet = await SeedQuizSet(ownerId: Guid.CreateVersion7());
        CurrentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new CreateGameRoomCommand(quizSet.Id));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("QuizSet.NotOwner");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesGameRoomAndReturnsGeneratedRoomCode()
    {
        // Arrange
        var ownerId = Guid.CreateVersion7();
        var quizSet = await SeedQuizSet(ownerId);
        CurrentUserMock.Setup(x => x.UserId).Returns(ownerId);
        _roomCodeGeneratorMock.Setup(x => x.GenerateUniqueCodeAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ABC123");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new CreateGameRoomCommand(quizSet.Id));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("ABC123");
        _gameRoomStoreMock.Verify(
            x => x.SaveAsync(
                It.Is<GameRoom>(gr => gr.RoomCode == "ABC123" && gr.HostId == ownerId && gr.QuizSetId == quizSet.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class JoinGameRoomCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IValidator<JoinGameRoomCommand>> _validatorMock = new();

    private JoinGameRoomCommandHandler CreateHandler()
        => new(_gameRoomStoreMock.Object, _currentUserMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<JoinGameRoomCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    // Build a real GameRoom aggregate through its public factory — no need for reflection or internal ctors.
    private static GameRoom CreateWaitingGameRoom(Guid hostId, string roomCode = "ABC123")
        => GameRoom.Create(new GameRoomCreationParams(roomCode, Guid.CreateVersion7(), hostId)).Value;

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsValidationErrorWithoutTouchingStore()
    {
        // Arrange
        var failure = new ValidationFailure("RoomCode", "Room code must be 6 characters long");
        _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<JoinGameRoomCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new JoinGameRoomCommand("AB", "Ivan"));

        // Assert
        result.IsError.Should().BeTrue();
        _gameRoomStoreMock.Verify(x => x.GetByRoomCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRoomDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        SetupValidatorSuccess();
        _gameRoomStoreMock
            .Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameRoom?)null);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new JoinGameRoomCommand("ABC123", "Ivan"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotFound");
    }

    [Fact]
    public async Task Handle_WhenAuthenticatedUserAlreadyJoined_ReturnsExistingParticipantIdWithoutSavingAgain()
    {
        // Arrange: simulate a reconnect — the same authenticated user rejoins after a network drop
        var userId = Guid.CreateVersion7();
        var gameRoom = CreateWaitingGameRoom(Guid.CreateVersion7());
        gameRoom.AddParticipant(userId, guestId: null, "Ivan");
        var existingParticipantId = gameRoom.Participants.Single().Id;

        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _gameRoomStoreMock
            .Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameRoom);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new JoinGameRoomCommand("ABC123", "Ivan"));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(existingParticipantId);
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAnonymousUserJoinsAvailableRoom_AddsGuestParticipantAndSaves()
    {
        // Arrange
        var gameRoom = CreateWaitingGameRoom(Guid.CreateVersion7());
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null); // anonymous / guest player
        _gameRoomStoreMock
            .Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameRoom);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new JoinGameRoomCommand("ABC123", "Guest99"));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        gameRoom.Participants.Should().ContainSingle(p => p.DisplayName == "Guest99" && p.GuestId != null);
        _gameRoomStoreMock.Verify(x => x.SaveAsync(gameRoom, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoomAlreadyStarted_ReturnsValidationErrorFromDomain()
    {
        // Arrange: GameRoom's own invariant (Status != Waiting) must bubble up through the handler untouched.
        // We legally transition the room to InProgress via the public Start() method — no reflection needed.
        var gameRoom = CreateWaitingGameRoom(Guid.CreateVersion7());
        gameRoom.AddParticipant(Guid.CreateVersion7(), null, "Host's friend"); // Start() requires >= 1 participant
        gameRoom.Start();

        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null); // a new guest tries to join late
        _gameRoomStoreMock
            .Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameRoom);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new JoinGameRoomCommand("ABC123", "LateJoiner"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotAcceptingParticipants");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
