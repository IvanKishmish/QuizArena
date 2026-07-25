namespace TelegramBot.Application.Contracts.Api;

public enum PowerUpType
{
    Freeze = 0,
    FiftyFifty = 1,
    DoubleOrNothing = 2
    // Steal intentionally omitted — removed from the QuizArena backend.
}

/// <summary>Server -> client "QuestionStarted". Deliberately has no IsCorrect field:
/// correctness is only known after SubmitAnswer, matching the API contract.</summary>
public sealed record QuestionStartedEvent(
    Guid QuestionId,
    string Text,
    int QuestionTypeRaw,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<QuestionStartedOption> Options);

public sealed record QuestionStartedOption(int Index, string Text);

public sealed record AnswerResultEvent(
    Guid QuestionId,
    Guid ParticipantId,
    bool IsCorrect,
    int PointsAwarded,
    int TotalScore);

public sealed record LeaderboardEntryEvent(Guid ParticipantId, string DisplayName, int Score, int Rank);

public sealed record LeaderboardUpdatedEvent(IReadOnlyList<LeaderboardEntryEvent> Standings);

public sealed record GameFinishedEvent(IReadOnlyList<LeaderboardEntryEvent> FinalStandings);

public sealed record ParticipantJoinedEvent(Guid ParticipantId, string DisplayName);

public sealed record PowerUpUsedEvent(Guid ParticipantId, PowerUpType PowerUpType, Guid? TargetParticipantId);

/// <summary>Sent on reconnect so a client that dropped mid-game can resume without state loss.</summary>
public sealed record GameStateRestoredEvent(
    QuestionStartedEvent? CurrentQuestion,
    IReadOnlyList<LeaderboardEntryEvent> Standings);
