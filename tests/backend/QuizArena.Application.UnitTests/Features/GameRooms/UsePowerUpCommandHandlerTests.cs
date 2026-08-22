using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.GameRooms;

public class UsePowerUpCommandHandlerTests
{
    private readonly Mock<IGameRoomStore> _gameRoomStoreMock = new();
    private readonly Mock<IGameNotifier> _gameNotifierMock = new();
    private readonly Mock<IConnectionTracker> _connectionTrackerMock = new();
    private readonly Mock<IQuestionStore> _questionStoreMock = new();
    private readonly Mock<IValidator<UsePowerUpCommand>> _validatorMock = new();

    private UsePowerUpCommandHandler CreateHandler() => new(
        _gameRoomStoreMock.Object,
        _gameNotifierMock.Object,
        _connectionTrackerMock.Object,
        _questionStoreMock.Object,
        _validatorMock.Object);

    private void SetupValidatorSuccess()
        => _validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<UsePowerUpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupSaveSucceeds()
        => _gameRoomStoreMock
            .Setup(x => x.SaveAsync(It.IsAny<GameRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    // Room with two participants, both granted their default power-ups via Start().
    private static (GameRoom Room, Participant Caster, Participant Target) CreateInProgressRoomWithTwoParticipants()
    {
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        room.AddParticipant(Guid.CreateVersion7(), null, "Caster");
        room.AddParticipant(Guid.CreateVersion7(), null, "Target");
        room.Start(); // grants default power-ups to every participant
        return (room, room.Participants[0], room.Participants[1]);
    }

    // Same as above, but also advances CurrentQuestionIndex from -1 (set by Start()) to 0, since
    // Start() alone leaves no "current question" — FiftyFifty needs one to have anything to eliminate.
    private static (GameRoom Room, Participant Caster, Participant Target) CreateInProgressRoomOnFirstQuestion()
    {
        var (room, caster, target) = CreateInProgressRoomWithTwoParticipants();
        room.NextQuestion();
        return (room, caster, target);
    }

    [Fact]
    public async Task Handle_WhenGameIsNotInProgress_ReturnsValidationError()
    {
        var room = GameRoom.Create(new GameRoomCreationParams("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7())).Value;
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(new UsePowerUpCommand("ABC123", Guid.CreateVersion7(), PowerUpType.Freeze, Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("GameRoom.NotInProgress");
    }

    [Fact]
    public async Task Handle_WhenParticipantNotFound_ReturnsNotFoundError()
    {
        var (room, _, _) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", Guid.CreateVersion7(), PowerUpType.Freeze, Guid.CreateVersion7()));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NotFound");
    }

    [Fact]
    public async Task Handle_FreezeWithoutTarget_ReturnsValidationError()
    {
        var (room, caster, _) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.Freeze, TargetParticipantId: null));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PowerUp.TargetRequired");
    }

    [Fact]
    public async Task Handle_FreezeWithUnknownTarget_ReturnsNotFoundError()
    {
        var (room, caster, _) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.Freeze, Guid.CreateVersion7())); // random, non-existent target

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.NotFound");
    }

    [Fact]
    public async Task Handle_FreezeTargetingSelf_ReturnsValidationError()
    {
        var (room, caster, _) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.Freeze, caster.Id)); // targeting self

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("PowerUp.CannotTargetSelf");
    }

    [Fact]
    public async Task Handle_WhenPowerUpAlreadyUsed_ReturnsErrorFromParticipantDomainRule()
    {
        // Arrange: caster already spent their Freeze earlier this game
        var (room, caster, target) = CreateInProgressRoomWithTwoParticipants();
        caster.UsePowerUp(PowerUpType.Freeze);
        SetupValidatorSuccess();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.Freeze, target.Id));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Participant.PowerUpNotAvailable");
    }

    [Fact]
    public async Task Handle_FreezeWithValidTarget_FreezesTargetAndNotifiesRoom()
    {
        // Arrange
        var (room, caster, target) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.Freeze, target.Id));

        // Assert
        result.IsError.Should().BeFalse();
        target.IsFrozen.Should().BeTrue();
        caster.AvailablePowerUps.Should().NotContain(PowerUpType.Freeze); // spent
        _gameRoomStoreMock.Verify(x => x.SaveAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _gameNotifierMock.Verify(
            x => x.PowerUpUsedAsync("ABC123", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoubleOrNothing_ActivatesFlagOnCasterWithoutRequiringTarget()
    {
        var (room, caster, _) = CreateInProgressRoomWithTwoParticipants();
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.DoubleOrNothing, TargetParticipantId: null));

        result.IsError.Should().BeFalse();
        caster.ActiveDoubleOrNothing.Should().Be(PowerUpType.DoubleOrNothing);
    }

    [Fact]
    public async Task Handle_FiftyFifty_EliminatesExactlyTwoWrongOptionsForFourOptionQuestion()
    {
        // Arrange: a question with 1 correct + 3 wrong options => min(2, 3-1) = 2 eliminated
        var (room, caster, _) = CreateInProgressRoomOnFirstQuestion();
        var question = Question.Create(new QuestionCreationParams(
            "2 + 2 = ?", QuestionType.SingleChoice, 30, 100,
            [
                new AnswerOptionParams("4", true, 0),
                new AnswerOptionParams("3", false, 1),
                new AnswerOptionParams("5", false, 2),
                new AnswerOptionParams("22", false, 3)
            ])).Value;
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([question]);
        _connectionTrackerMock
            .Setup(x => x.GetConnectionAsync(caster.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("connection-123");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.FiftyFifty, TargetParticipantId: null));

        // Assert
        result.IsError.Should().BeFalse();
        _gameNotifierMock.Verify(
            x => x.SendToParticipantAsync(
                "connection-123",
                "FiftyFiftyApplied",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_FiftyFifty_WhenParticipantHasNoActiveConnection_SkipsDirectNotificationButStillSucceeds()
    {
        // Arrange: participant disconnected right after using the power-up (edge case)
        var (room, caster, _) = CreateInProgressRoomOnFirstQuestion();
        var question = Question.Create(new QuestionCreationParams(
            "2 + 2 = ?", QuestionType.SingleChoice, 30, 100,
            [
                new AnswerOptionParams("4", true, 0),
                new AnswerOptionParams("3", false, 1),
                new AnswerOptionParams("5", false, 2)
            ])).Value;
        SetupValidatorSuccess();
        SetupSaveSucceeds();
        _gameRoomStoreMock.Setup(x => x.GetByRoomCodeAsync("ABC123", It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _questionStoreMock
            .Setup(x => x.GetByQuizSetIdAsync(room.QuizSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([question]);
        _connectionTrackerMock
            .Setup(x => x.GetConnectionAsync(caster.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(
            new UsePowerUpCommand("ABC123", caster.Id, PowerUpType.FiftyFifty, TargetParticipantId: null));

        // Assert
        result.IsError.Should().BeFalse();
        _gameNotifierMock.Verify(
            x => x.SendToParticipantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The room-wide notification still fires regardless of the direct one
        _gameNotifierMock.Verify(
            x => x.PowerUpUsedAsync("ABC123", It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}