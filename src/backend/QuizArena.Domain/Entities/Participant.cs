using QuizArena.Domain.Common;
using ErrorOr;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;

namespace QuizArena.Domain.Entities;

public sealed class Participant : TransientEntity
{
    public string DisplayName { get; private init; } = string.Empty;
    public Guid? UserId { get; private init; }
    public Guid? GuestId { get; private init; }
    public int Score { get; private set; }

    private readonly List<PowerUpType> _availablePowerUps = [];
    public IReadOnlyList<PowerUpType> AvailablePowerUps => _availablePowerUps.AsReadOnly();
    
    public PowerUpType? ActiveDoubleOrNothing { get; private set; }
    public bool IsFrozen { get; private set; }
    public DateTimeOffset? FrozenUntil { get; private set; }

    private Participant()
    { } // ef

    private Participant(Guid id, Guid? userId, Guid? guestId, string displayName) : base(id)
    {
        UserId = userId;
        GuestId = guestId;
        DisplayName = displayName;
    }
    
    internal Participant(Guid id, ParticipantCreationParams args) : base(id)
    {
        DisplayName = args.DisplayName;
        UserId = args.UserId;
        GuestId = args.GuestId;
        Score = args.Score;
        _availablePowerUps = args.AvailablePowerUp.ToList();
        ActiveDoubleOrNothing = args.ActiveDoubleOrNothing;
        IsFrozen = args.IsFrozen;
        FrozenUntil = args.FrozenUntil;
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

        var finalPoints = ActiveDoubleOrNothing == PowerUpType.DoubleOrNothing
            ? points * 2
            : points;

        Score += finalPoints;
        ActiveDoubleOrNothing = null;

        return Result.Updated;
    }

    public void GrantDefaultPowerUps()
    {
        _availablePowerUps.Clear();
        _availablePowerUps.AddRange(Enum.GetValues<PowerUpType>());
    }

    public ErrorOr<Updated> UsePowerUp(PowerUpType type)
    {
        if (!_availablePowerUps.Contains(type))
            return Error.Validation("Participant.PowerUpNotAvailable", "This power-up is not available or already used.");
        
        _availablePowerUps.Remove(type);

        if (type == PowerUpType.DoubleOrNothing)
            ActiveDoubleOrNothing = type;
        
        return Result.Updated;
    }

    public void ApplyFreeze(TimeSpan duration)
    {
        IsFrozen = true;
        FrozenUntil = DateTimeOffset.UtcNow.Add(duration);
    }

    public void ClearFreezeIfExpired()
    {
        if (IsFrozen && FrozenUntil is not null && DateTimeOffset.UtcNow >= FrozenUntil)
        {
            IsFrozen = false;
            FrozenUntil = null;
        }
    }
}