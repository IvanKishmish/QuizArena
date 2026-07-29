using FluentValidation.TestHelper;
using QuizArena.Application.Features.GameRooms.Commands.EndGame;
using QuizArena.Application.Features.GameRooms.Commands.JoinGameRoom;
using QuizArena.Application.Features.GameRooms.Commands.NextQuestion;
using QuizArena.Application.Features.GameRooms.Commands.StartGame;
using QuizArena.Application.Features.GameRooms.Commands.SubmitAnswer;
using QuizArena.Application.Features.GameRooms.Commands.UsePowerUp;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.UnitTests.Features.GameRooms.Validators;

public class JoinGameRoomCommandValidatorTests
{
    private readonly JoinGameRoomCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("ABC12")]  // 5 chars — too short
    [InlineData("ABC1234")] // 7 chars — too long
    public void Validate_WithInvalidRoomCode_HasValidationErrorForRoomCode(string roomCode)
    {
        var command = new JoinGameRoomCommand(roomCode, "Ivan");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithEmptyDisplayName_HasValidationErrorForDisplayName()
    {
        var command = new JoinGameRoomCommand("ABC123", "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_WithDisplayNameExceedingMaxLength_HasValidationErrorForDisplayName()
    {
        var command = new JoinGameRoomCommand("ABC123", new string('a', 31));

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var command = new JoinGameRoomCommand("ABC123", "Ivan");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

// StartGame, NextQuestion, EndGame all validate RoomCode through the exact same
// shared extension method (RoomCodeValidationRule.SetRoomCode()). We still give each
// its own minimal test — this protects against someone accidentally detaching a handler's
// validator from the shared rule during a future refactor.
public class StartGameCommandValidatorTests
{
    private readonly StartGameCommandValidator _validator = new();

    [Fact]
    public void Validate_WithRoomCodeNotSixCharacters_HasValidationErrorForRoomCode()
    {
        var result = _validator.TestValidate(new StartGameCommand("ABC"));

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithValidRoomCode_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(new StartGameCommand("ABC123"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class NextQuestionCommandValidatorTests
{
    private readonly NextQuestionCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyRoomCode_HasValidationErrorForRoomCode()
    {
        var result = _validator.TestValidate(new NextQuestionCommand(""));

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithValidRoomCode_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(new NextQuestionCommand("ABC123"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class EndGameCommandValidatorTests
{
    private readonly EndGameCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyRoomCode_HasValidationErrorForRoomCode()
    {
        var result = _validator.TestValidate(new EndGameCommand(""));

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithValidRoomCode_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(new EndGameCommand("ABC123"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class SubmitAnswerCommandValidatorTests
{
    private readonly SubmitAnswerCommandValidator _validator = new();

    private static SubmitAnswerCommand CreateValidCommand() =>
        new("ABC123", Guid.CreateVersion7(), Guid.CreateVersion7(), [0, 1]);

    [Fact]
    public void Validate_WithInvalidRoomCode_HasValidationErrorForRoomCode()
    {
        var command = CreateValidCommand() with { RoomCode = "X" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithEmptyParticipantId_HasValidationErrorForParticipantId()
    {
        var command = CreateValidCommand() with { ParticipantId = Guid.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Fact]
    public void Validate_WithEmptyQuestionId_HasValidationErrorForQuestionId()
    {
        var command = CreateValidCommand() with { QuestionId = Guid.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.QuestionId);
    }

    [Fact]
    public void Validate_WithNullSelectedOptionIndices_HasValidationErrorForSelectedOptionIndices()
    {
        var command = CreateValidCommand() with { SelectedOptionIndices = null! };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SelectedOptionIndices);
    }

    [Fact]
    public void Validate_WithEmptySelectedOptionIndicesList_HasNoValidationError()
    {
        // Arrange: the rule is only NotNull(), not NotEmpty() — an empty list (e.g. "time
        // ran out, no answer selected") is intentionally a valid, scoreable submission.
        var command = CreateValidCommand() with { SelectedOptionIndices = [] };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.SelectedOptionIndices);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(CreateValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class UsePowerUpCommandValidatorTests
{
    private readonly UsePowerUpCommandValidator _validator = new();

    private static UsePowerUpCommand CreateValidCommand() =>
        new("ABC123", Guid.CreateVersion7(), PowerUpType.Freeze, Guid.CreateVersion7());

    [Fact]
    public void Validate_WithInvalidRoomCode_HasValidationErrorForRoomCode()
    {
        var command = CreateValidCommand() with { RoomCode = "" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.RoomCode);
    }

    [Fact]
    public void Validate_WithEmptyParticipantId_HasValidationErrorForParticipantId()
    {
        var command = CreateValidCommand() with { ParticipantId = Guid.Empty };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Fact]
    public void Validate_WithUndefinedPowerUpType_HasValidationErrorForPowerUpType()
    {
        // Arrange: a value outside the enum's defined range (IsInEnum() rule)
        var command = CreateValidCommand() with { PowerUpType = (PowerUpType)999 };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PowerUpType);
    }

    [Fact]
    public void Validate_WithoutTargetParticipantId_HasNoValidationError()
    {
        // Arrange: TargetParticipantId is nullable at the validator level — the "Freeze
        // requires a target" rule lives in the handler/domain, not here. Worth documenting,
        // since it's easy to assume the validator enforces this.
        var command = CreateValidCommand() with { TargetParticipantId = null };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.TargetParticipantId);
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(CreateValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }
}
