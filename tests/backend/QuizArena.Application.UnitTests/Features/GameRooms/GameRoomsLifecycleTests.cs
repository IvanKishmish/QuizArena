using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameHistory.Commands.SaveGameHistory;
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

    // K4: every mutation now goes through OptimisticConcurrency, which calls SaveAsync — an unconfigured
    // mock defaults to `false` (conflict), so any test whose happy path should actually succeed needs this.
    private void SetupSaveSucceeds()
        => _gameRoomStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
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
        SetupSaveSucceeds();
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new StartGameCommand("ABC123"));

        result.IsError.Should().BeFalse();
        room.Status.Should().Be(GameRoomStatus.InProgress);
        // S11: Start() no longer puts the room "on" question 0 — see NextQuestionCommandHandlerTests for why.
        room.CurrentQuestionIndex.Should().Be(-1);
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

    private void SetupSaveSucceeds()
        => _gameRoomStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        // Arrange: room still Waiting — GameRoom.NextQuestion() itself rejects this. Even though this
        // handler now checks for a next *question* before mutating the room (see the test below), it still
        // has to call GameRoom.NextQuestion() to surface this particular invariant, since "is the game even
        // running" is something only the room itself knows.
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        // Waiting room has no questions to advance to either way, but the point of this test is the
        // "NotInProgress" domain check, not "NotFound" — give it a question so that isn't what trips first.
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateQuestion()]);
        var handler = CreateHandler();

        var result = await handler.Handle(new NextQuestionCommand("ABC123"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }

    [Fact]
    public async Task Handle_WhenNoMoreQuestionsRemain_ReturnsNotFoundError_WithoutTouchingRoomState()
    {
        // S11/K4: the "is there a next question" check now happens *before* GameRoom.NextQuestion() is
        // called — the old handler incremented+saved CurrentQuestionIndex first and only found out
        // afterwards that there was nothing to advance to, leaving the room parked past the end of the quiz
        // for no reason. Now a "no more questions" call leaves the room's state (and Redis) untouched.
        var room = CreateInProgressRoom();
        var originalIndex = room.CurrentQuestionIndex;
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
        room.CurrentQuestionIndex.Should().Be(originalIndex); // untouched
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
        _gameNotifierMock.Verify(
            x => x.QuestionStartedAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNextQuestionAvailable_AdvancesAndNotifiesParticipants()
    {
        // Arrange
        var room = CreateInProgressRoom(); // S11: CurrentQuestionIndex starts at -1 after Start()
        var firstQuestion = CreateQuestion("First");
        var secondQuestion = CreateQuestion("Second");
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstQuestion, secondQuestion]);
        var handler = CreateHandler();

        // Act: NextQuestion() bumps CurrentQuestionIndex from -1 to 0 => "First" is served — this is
        // precisely the S11 fix: question #0 now actually gets sent to someone.
        var result = await handler.Handle(new NextQuestionCommand("ABC123"));

        // Assert
        result.IsError.Should().BeFalse();
        room.CurrentQuestionIndex.Should().Be(0);
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

    private void SetupSaveSucceeds()
        => _gameRoomStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    // W3: EndGame now saves Finish() through OptimisticConcurrency, and only afterwards sends
    // SaveGameHistoryCommand — a happy-path test has to make that command succeed too, or the handler
    // correctly (per W3's own fix) stops and never gets to publishing GameFinishedNotification.
    private void SetupSaveGameHistorySucceeds()
        => _mediatorMock
            .Setup(x => x.Send(It.IsAny<SaveGameHistoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

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
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidHostAndInProgressRoom_SavesFinishedRoomBeforePublishingNotification()
    {
        // W3: this is the actual fix under test — Finish() being *persisted*, not just called in memory.
        // Arrange
        var hostId = Guid.CreateVersion7();
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        room.Start();
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        SetupSaveGameHistorySucceeds();
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
        // W3: the whole point — Finish() is now saved, not just mutated in memory and left to CleanupGameRoomHandler
        // (running at some unspecified point relative to everything else) to be the only reason it stuck.
        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(
            x => x.Send(
                It.Is<SaveGameHistoryCommand>(c => c.GameId == room.Id && c.QuizSetId == room.QuizSetId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mediatorMock.Verify(
            x => x.Publish(
                It.Is<GameFinishedNotification>(n =>
                    n.RoomCode == "ABC123" &&
                    n.QuizSetId == room.QuizSetId &&
                    n.FinalLeaderboard == finalLeaderboard),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSaveGameHistoryFails_ReturnsErrorAndDoesNotPublishNotification()
    {
        // W3/W6: SaveGameHistoryCommand is sent as a direct command specifically so its failure can stop
        // EndGame from telling anyone the game finished successfully — unlike the old design where email/
        // history/cleanup were independent notification handlers with no relationship to each other's outcome.
        var hostId = Guid.CreateVersion7();
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), hostId)).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Player1");
        room.Start();
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _mediatorMock
            .Setup(x => x.Send(It.IsAny<SaveGameHistoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ErrorOr<Success>.From(
                [Error.Failure("GameHistory.SaveFailed", "Could not save game history.")]));
        _currentUserMock.Setup(x => x.UserId).Returns(hostId);
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", room.Participants.Count, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LeaderboardEntry(room.Participants[0].Id, "Player1", 100)]);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new EndGameCommand("ABC123"));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameHistory.SaveFailed");
        _mediatorMock.Verify(
            x => x.Publish(It.IsAny<GameFinishedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
