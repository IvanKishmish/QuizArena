using ErrorOr;
using QuizArena.Domain.Common;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Domain.Entities;

public sealed class GameHistoryEntry : Entity
{
    public Guid QuizSetId { get; private init; }
    public Guid? ParticipantUserId { get; private init; }
    public string DisplayName { get; private init; } = string.Empty;
    public int FinalScore { get; private init; }
    public int Placement { get; private init; }

    private GameHistoryEntry()
    { } //ef

    private GameHistoryEntry(Guid id, GameHistoryEntryCreationParams args) : base(id)
    {
        QuizSetId = args.QuizSetId;
        ParticipantUserId = args.ParticipantUserId;
        DisplayName = args.DisplayName;
        FinalScore = args.FinalScore;
        Placement = args.Placement;
    }
    
    public static ErrorOr<GameHistoryEntry> Create(GameHistoryEntryCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new GameHistoryEntry(Guid.CreateVersion7(), args);
    }

    private static ErrorOr<Success> ValidateInvariants(GameHistoryEntryCreationParams args)
    {
        var errors = new List<Error>();

        if (args.QuizSetId == Guid.Empty)
            errors.Add(Error.Validation("GameHistoryEntry.QuizSetIdRequired", "QuizSetId is required"));

        if (string.IsNullOrWhiteSpace(args.DisplayName))
            errors.Add(Error.Validation("GameHistoryEntry.DisplayNameRequired", "Display name is required"));

        if (args.FinalScore < 0)
            errors.Add(Error.Validation("GameHistoryEntry.InvalidScore", "Final score cannot be negative"));

        if (args.Placement <= 0)
            errors.Add(Error.Validation("GameHistoryEntry.InvalidPlacement", "Placement must be greater than zero"));

        return errors.Count > 0 ? errors : Result.Success;
    }
}