namespace QuizArena.Domain.Entities.Models;

public sealed record GameHistoryEntryCreationParams(
    Guid QuizSetId,
    Guid? ParticipantUserId,
    string DisplayName,
    int FinalScore,
    int Placement);