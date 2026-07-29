using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;
using QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;
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

    // A room started but NextQuestion() never called: CurrentQuestionStartedAt stays null.
    // The handler then falls back to elapsedSeconds = question.TimeLimitSeconds (worst case),
    // which makes the resulting score fully deterministic — perfect for assertions.
    private static (GameRoom Room, Participant Participant) CreateInProgressRoomWithOneParticipant()
    {
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Ivan");
        room.Start();
        return (room, room.Participants.Single());
    }

    private static Question CreateSingleChoiceQuestion(int points = 100, int timeLimitSeconds = 30)
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
        var (room, _) = CreateInProgressRoomWithOneParticipant();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7(), [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NotFound");
    }

    [Fact]
    public async Task Handle_WhenParticipantIsStillFrozen_ReturnsValidationErrorAndDoesNotScore()
    {
        // Arrange
        var (room, participant) = CreateInProgressRoomWithOneParticipant();
        participant.ApplyFreeze(TimeSpan.FromMinutes(5)); // far from expiring
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, Guid.CreateVersion7(), [0]));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.Frozen");
        _gameRoomStoreMock.Verify(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()), Times.Never);
        _questionStoreMock.Verify(
            x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFreezeHasExpired_ClearsItAndProceedsToScoreNormally()
    {
        // Arrange: freeze duration is effectively over by the time we submit
        var (room, participant) = CreateInProgressRoomWithOneParticipant();
        participant.ApplyFreeze(TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);

        var question = CreateSingleChoiceQuestion();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByIdAsync(room.QuizSetId, question.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0])); // correct option

        // Assert
        result.IsError.Should().BeFalse();
        participant.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenQuestionDoesNotExist_ReturnsNotFoundError()
    {
        var (room, participant) = CreateInProgressRoomWithOneParticipant();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByIdAsync(room.QuizSetId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Question?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new SubmitAnswerCommand("ABC123", participant.Id, Guid.CreateVersion7(), [0]));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Question.NotFound");
    }

    [Fact]
    public async Task Handle_WithCorrectAnswer_AwardsHalfPointsWhenNoTimingDataAvailable()
    {
        // Arrange: CurrentQuestionStartedAt is null (NextQuestion was never called),
        // so elapsedSeconds == TimeLimitSeconds => speed bonus is 0 => score == Points / 2.
        var (room, participant) = CreateInProgressRoomWithOneParticipant();
        var question = CreateSingleChoiceQuestion(points: 100);
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByIdAsync(room.QuizSetId, question.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);
        _leaderboardStoreMock
            .Setup(x => x.GetTopAsync("ABC123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new SubmitAnswerCommand("ABC123", participant.Id, question.Id, [0])); // index 0 == correct option "4"

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(50); // 100 / 2, no speed bonus
        participant.Score.Should().Be(50);

        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _leaderboardStoreMock.Verify(
            x => x.UpdateScoreAsync("ABC123", participant.Id, participant.DisplayName, 50, It.IsAny<CancellationToken>()),
            Times.Once);
        _gameNotifierMock.Verify(
            x => x.LeaderboardUpdatedAsync("ABC123", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithIncorrectAnswer_ReturnsZeroScoreButStillUpdatesLeaderboard()
    {
        // Arrange
        var (room, participant) = CreateInProgressRoomWithOneParticipant();
        var question = CreateSingleChoiceQuestion(points: 100);
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByIdAsync(room.QuizSetId, question.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);
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
    public async Task Handle_RequestsOnlyUpToFiveTopEntriesRegardlessOfParticipantCount()
    {
        // Arrange: a room with more than 5 participants — leaderboard call must still be capped at 5
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        for (var i = 0; i < 7; i++)
            room.AddParticipant(Guid.CreateVersion7(), null, $"Player{i}");
        room.Start();
        var participant = room.Participants.First();

        var question = CreateSingleChoiceQuestion();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByIdAsync(room.QuizSetId, question.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(question);
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
