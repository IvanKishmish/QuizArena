using QuizArena.Domain.Enums;

namespace QuizArena.Domain.Entities.Models;

public sealed record ParticipantCreationParams(
    string DisplayName,
    Guid? UserId,
    Guid? GuestId,
    int Score,
    IReadOnlyList<PowerUpType> AvailablePowerUp,
    PowerUpType? ActiveDoubleOrNothing,
    bool IsFrozen,
    DateTimeOffset? FrozenUntil);