using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;
using QuizArena.Application.UnitTests.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.GameRooms;

public class SubmitAnswerCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<IQuestionStore> _questionStoreMock = new();
    private readonly Mock<ILeaderboardStore> _leaderboardStoreMock = new();
    private readonly Mock<IGameNotifier> _gameNotifierMock = new();
    private readonly Mock<IValidator<SubmitAnswerCommand>> _validatorMock = new();

    private SubmitAnswerCommandHandler CreateHandler() => new(
        _gameRoomStoreMock.Object,
        _questionStoreMock.Object,
        _leaderboardStoreMock.Object,
        _gameNotifierMock.Object,
        _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<SubmitAnswerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    // K4: every mutation now goes through OptimisticConcurrency, which calls SaveAsync — an unconfigured
    // mock defaults to `false` (conflict), so any test whose happy path should actually succeed needs this.
    private void SetupSaveSucceeds()
        => _gameRoomStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    // K2: the room now only accepts an answer for its own CurrentQuestionIndex, verified against
    // IQuestionStore.GetByQuizSetIdAsync — not "any question the client names that happens to exist"
    // (IQuestionStore.GetByIdAsync isn't even called by this handler anymore). Room.Start() leaves
    // CurrentQuestionIndex at -1 (S11), so a real "current question" requires an explicit NextQuestion() call.
    private (GameRoom Room, Participant Participant) CreateInProgressRoomWithCurrentQuestion(Question currentQuestion)
    {
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();

        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([currentQuestion]);

        room.NextQuestion(); // -1 -> 0: currentQuestion is now CurrentQuestionIndex's question

        return (room, room.Participants.Single());
    }

    // TimeLimitSeconds deliberately huge: CurrentQuestionStartedAt is always "now" once NextQuestion() has
    // been called (there's no way around the clock from the handler's public surface — see the review
    // response for why a full TimeProvider threading wasn't worth it here), so a test needs the time
    // *actually elapsed while it ran* to be negligible relative to the limit for the score to be reliably
    // "full points". A few milliseconds against 100,000 seconds comfortably rounds away to nothing.
    private static Question CreateSingleChoiceQuestion(int points = 100, int timeLimitSeconds = 100_000)
    {
        var options = new List<AnswerOptionParams>
        {
            new("4", true, 0),
            new("5", false, 1)
        };
        return Question.Create(new QuestionCreationParams(
            "2 + 2 = ?", QuestionType.SingleChoice, timeLimitSeconds, points, options)).Value;
    }

    [Fact]
    public async Task Handle_WhenRoomDoesNotExist_ReturnsNotFoundError()
    {
        SetupValidatorSuccess();
        _gameRoomStoreMock
            .Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameRoom?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7(), [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotFound");
    }

    [Fact]
    public async Task Handle_WhenGameIsNotInProgress_ReturnsValidationError()
    {
        // Arrange: room still in Waiting state — game hasn't started yet
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7(), [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }

    [Fact]
    public async Task Handle_WhenParticipantNotInRoom_ReturnsNotFoundError()
    {
        var question = CreateSingleChoiceQuestion();
        var (room, _) = CreateInProgressRoomWithCurrentQuestion(question);
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", Guid.CreateVersion7(), question.Id, [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenParticipantIsStillFrozen_ReturnsValidationErrorAndDoesNotScore()
    {
        // Arrange
        var question = CreateSingleChoiceQuestion();
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(question);
        participant.ApplyFreeze(TimeSpan.FromMinutes(5)); // far from expiring
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0]));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.Frozen");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFreezeHasExpired_ClearsItAndProceedsToScoreNormally()
    {
        // S13: no more `await Task.Delay(30)` — a TimeProvider fixed an hour in the past makes FrozenUntil
        // already-expired deterministically (see Participant.ApplyFreeze / ParticipantTests for the domain
        // level version of this same fix).
        // Arrange
        var question = CreateSingleChoiceQuestion();
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(question);
        var anHourAgo = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
        participant.ApplyFreeze(TimeSpan.Zero, anHourAgo);

        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0])); // correct option

        // Assert
        result.IsError.Should().BeFalse();
        participant.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenSubmittedQuestionIsNotTheCurrentQuestion_ReturnsValidationError()
    {
        // K2: this is the actual vulnerability under test — answering a question that isn't the one the
        // room is currently showing (e.g. the previous or a future one) must be rejected, regardless of
        // whether that question exists in the quiz.
        var currentQuestion = CreateSingleChoiceQuestion();
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(currentQuestion);
        var someOtherQuestionId = Guid.CreateVersion7();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, someOtherQuestionId, [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.NotCurrent");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNoQuestionIsCurrentlyActive_ReturnsNotFoundError()
    {
        // Arrange: game started but the host hasn't called NextQuestion() yet (S11: CurrentQuestionIndex == -1)
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        var participant = room.Participants.Single();

        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", participant.Id, Guid.CreateVersion7(), [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.NotFound");
    }

    [Fact]
    public async Task Handle_WithCorrectAnswer_AwardsPositiveScoreAndUpdatesLeaderboard()
    {
        // Arrange
        var question = CreateSingleChoiceQuestion(points: 100);
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(question);
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0])); // index 0 == correct option "4"

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(100); // negligible elapsed time against a huge TimeLimitSeconds => ~full points
        participant.Score.Should().Be(100);

        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _leaderboardStoreMock.Verify(
            x => x.UpdateScoreAsync("ABC123", participant.Id, participant.DisplayName, 100, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameNotifierMock.Verify(
            x => x.LeaderboardUpdatedAsync("ABC123", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithIncorrectAnswer_ReturnsZeroScoreButStillUpdatesLeaderboard()
    {
        // Arrange
        var question = CreateSingleChoiceQuestion(points: 100);
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(question);
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        // Act: index 1 == wrong option "5"
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [1]));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0);
        participant.Score.Should().Be(0);
        _leaderboardStoreMock.Verify(
            x => x.UpdateScoreAsync("ABC123", participant.Id, participant.DisplayName, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheSameQuestionIsAnsweredTwice_RejectsTheSecondSubmissionAndDoesNotDoubleScore()
    {
        // K2: this is the idempotency half of the fix — a replayed/duplicated request for a question the
        // participant already answered must not add to their score again.
        // Arrange
        var question = CreateSingleChoiceQuestion(points: 100);
        var (room, participant) = CreateInProgressRoomWithCurrentQuestion(question);
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        var command = new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0]);
        await handler.Handle(command);

        // Act: submit the exact same answer again
        var secondResult = await handler.Handle(command);

        // Assert
        secondResult.IsError.Should().BeTrue();
        secondResult.FirstError.Code.Should().Be("Participant.AlreadyAnswered");
        participant.Score.Should().Be(100); // not 200
    }

    [Fact]
    public async Task Handle_RequestsOnlyUpToFiveTopEntriesRegardlessOfParticipantCount()
    {
        // Arrange: a room with more than 5 participants — leaderboard call must still be capped at 5
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        for (var i = 0; i < 7; i++)
            room.AddParticipant(Guid.CreateVersion7(), null, $"Player{i}");
        room.Start();

        var question = CreateSingleChoiceQuestion();
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([question]);
        room.NextQuestion();

        var participant = room.Participants.First();

        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        // Act
        await handler.Handle(new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0]));

        // Assert
        _leaderboardStoreMock.Verify(x => x.GetTopAsync("ABC123", 5, It.IsAny<CancellationToken>()), Times.Once);
    }
}
