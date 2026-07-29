using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameRooms.Commands.EndGame;
using QuizArena.Application.Features.GameRooms.Commands.NextQuestion;
using QuizArena.Application.Features.GameRooms.Commands.StartGame;
using QuizArena.Application.Features.GameRooms.Events;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.GameRooms;

public class StartGameCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IValidator<StartGameCommand>> _validatorMock = new();

    private StartGameCommandHandler CreateHandler()
        => new(_gameRoomStoreMock.Object, _currentUserMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<StartGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private static GameRoom CreateWaitingRoom(Guid hostId)
        => GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorizedError()
    {
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new StartGameCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.NotAuthenticated");
    }

    [Fact]
    public async Task Handle_WhenUserIsNotHost_ReturnsForbiddenError()
    {
        var room = CreateWaitingRoom(hostId: Guid.CreateVersion7());
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7()); // someone else, not the host
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new StartGameCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotHost");
    }

    [Fact]
    public async Task Handle_WhenRoomHasNoParticipants_ReturnsValidationErrorFromDomain()
    {
        // Arrange: GameRoom.Start() itself refuses to start an empty room
        var hostId = Guid.CreateVersion7();
        var room = CreateWaitingRoom(hostId);
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new StartGameCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotEnoughParticipants");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidHostAndParticipants_StartsGameAndSaves()
    {
        var hostId = Guid.CreateVersion7();
        var room = CreateWaitingRoom(hostId);
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new StartGameCommand("ABC123"));

        result.IsError.Should().BeFalse();
        room.Status.Should().Be(GameRoomStatus.InProgress);
        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class NextQuestionCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<IQuestionStore> _questionStoreMock = new();
    private readonly Mock<IGameNotifier> _gameNotifierMock = new();
    private readonly Mock<IValidator<NextQuestionCommand>> _validatorMock = new();

    private NextQuestionCommandHandler CreateHandler()
        => new(_gameRoomStoreMock.Object, _questionStoreMock.Object, _gameNotifierMock.Object, _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<NextQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private static GameRoom CreateInProgressRoom()
    {
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        room.Start();
        return room;
    }

    private static Question CreateQuestion(string text = "Q1")
        => Question.Create(new QuestionCreationParams(
            text, QuestionType.SingleChoice, 30, 100,
            [new AnswerOptionParams("A", true, 0), new AnswerOptionParams("B", false, 1)])).Value;

    [Fact]
    public async Task Handle_WhenGameIsNotInProgress_ReturnsValidationErrorFromDomain()
    {
        // Arrange: room still Waiting — GameRoom.NextQuestion() itself rejects this
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new NextQuestionCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }

    [Fact]
    public async Task Handle_WhenNoMoreQuestionsRemain_ReturnsNotFoundError_EvenThoughRoomStateWasAlreadyAdvanced()
    {
        // Arrange: this documents a real handler quirk worth knowing — the room is saved
        // with the advanced CurrentQuestionIndex BEFORE the "no more questions" check runs.
        var room = CreateInProgressRoom();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]); // no questions in the store at all
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new NextQuestionCommand("ABC123"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.NotFound");
        room.CurrentQuestionIndex.Should().Be(1); // state was already advanced and saved
        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _gameNotifierMock.Verify(
            x => x.QuestionStartedAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNextQuestionAvailable_AdvancesAndNotifiesParticipants()
    {
        // Arrange
        var room = CreateInProgressRoom(); // CurrentQuestionIndex starts at 0
        var firstQuestion = CreateQuestion("First");
        var secondQuestion = CreateQuestion("Second");
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstQuestion, secondQuestion]);
        var handler = CreateHandler();

        // Act: NextQuestion() bumps CurrentQuestionIndex from 0 to 1 => "Second" is served
        var result = await handler.Handle(new NextQuestionCommand("ABC123"));

        // Assert
        result.IsError.Should().BeFalse();
        room.CurrentQuestionStartedAt.Should().NotBeNull();
        _gameNotifierMock.Verify(
            x => x.QuestionStartedAsync("ABC123", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class EndGameCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<ILeaderboardStore> _leaderboardStoreMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IValidator<EndGameCommand>> _validatorMock = new();

    private EndGameCommandHandler CreateHandler() => new(
        _gameRoomStoreMock.Object,
        _leaderboardStoreMock.Object,
        _mediatorMock.Object,
        _currentUserMock.Object,
        _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<EndGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    [Fact]
    public async Task Handle_WhenUserIsNotHost_ReturnsForbiddenError()
    {
        var hostId = Guid.CreateVersion7();
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.CreateVersion7());
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new EndGameCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotHost");
    }

    [Fact]
    public async Task Handle_WhenRoomIsNotInProgress_ReturnsValidationErrorFromDomain()
    {
        // Arrange: room never started — Finish() rejects a non-InProgress room
        var hostId = Guid.CreateVersion7();
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new EndGameCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }

    [Fact]
    public async Task Handle_WithValidHostAndInProgressRoom_FinishesGameAndPublishesGameFinishedNotification()
    {
        // Arrange
        var hostId = Guid.CreateVersion7();
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        room.Start();
        SetupValidatorSuccess();
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var finalLeaderboard = new List<LeaderboardEntry> { new(room.Participants[0].Id, "Player1", 100) };
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", room.Participants.Count, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalLeaderboard);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new EndGameCommand("ABC123"));

        // Assert
        result.IsError.Should().BeFalse();
        room.Status.Should().Be(GameRoomStatus.Finished);
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<GameFinishedNotification>(n =>
                    n.RoomCode == "ABC123" &&
                    n.QuizSetId == room.QuizSetId &&
                    n.FinalLeaderboard == finalLeaderboard),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
