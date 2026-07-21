using QuizArena.Domain.Common;
using ErrorOr;

namespace QuizArena.Domain.Entities;

public sealed class Participant : TransientEntity
{
    public string DisplayName { get; private init; } = string.Empty;

    public Guid? UserId { get; private init; }

    public Guid? GuestId { get; private init; }

    public int Score { get; private set; }

    private Participant()
    { } // ef

    private Participant(Guid id, Guid? userId, Guid? guestId, string displayName) : base(id)
    {
        UserId = userId;
        GuestId = guestId;
        DisplayName = displayName;
    }
    
    internal Participant(Guid id, string displayName, Guid? userId, Guid? guestId, int score) : base(id)
    {
        DisplayName = displayName;
        UserId = userId;
        GuestId = guestId;
        Score = score;
    }
    
    public static ErrorOr<Participant> Create(Guid? userId, Guid? guestId, string displayName)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(displayName))
            errors.Add(Error.Validation("Participant.DisplayNameRequired", "Display name is required"));

        if (userId is null && guestId is null)
            errors.Add(Error.Validation("Participant.IdentityRequired", "Either UserId or GuestId must be provided"));

        if (userId is not null && guestId is not null)
            errors.Add(Error.Validation("Participant.ConflictingIdentity", "A participant cannot be both a registered user and a guest"));

        if (errors.Count > 0)
            return errors;

        return new Participant(Guid.CreateVersion7(), userId, guestId, displayName);
    }

    public ErrorOr<Updated> AddScore(int points)
    {
        if (points < 0)
            return Error.Validation("Participant.NegativeScore", "Points to add cannot be negative");

        Score += points;

        return Result.Updated;
    }
}