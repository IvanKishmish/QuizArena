namespace TelegramBot.Application.Conversations;

/// <summary>
/// Which multi-step dialog a chat is currently inside, if any. Kept as a flat enum
/// (rather than a class hierarchy) because the number of flows is small and the
/// pipeline needs to make a cheap "is this chat mid-flow?" check on every update.
/// </summary>
public enum ConversationFlow
{
    None = 0,
    Registering,
    LoggingIn,
    CreatingQuiz,
    EditingQuiz,
    AddingQuestion,
    AwaitingRoomCodeToJoin,
    AwaitingGuestDisplayName,
    AwaitingBroadcastMessage,
    AwaitingBroadcastConfirmation
}

/// <summary>
/// Which field within the current flow we're waiting for the next message to fill.
/// Combined with <see cref="ConversationFlow"/> this is the whole FSM: (flow, step) -> handler.
/// </summary>
public enum ConversationStep
{
    None = 0,

    // Registering / LoggingIn
    NickName,
    Email,
    Password,

    // CreatingQuiz
    QuizTitle,
    QuizDescription,

    // EditingQuiz (same shape as CreatingQuiz, reuses ConversationData.QuizTitle/QuizDescription)
    EditQuizTitle,
    EditQuizDescription,

    // AddingQuestion (loop: Text -> Type -> Options... -> next question or finish)
    QuestionText,
    QuestionType,
    QuestionTimeLimit,
    QuestionPoints,
    QuestionOptionText,
    QuestionOptionIsCorrect,

    RoomCode,
    GuestDisplayName,

    BroadcastMessage,
    BroadcastConfirmation
}

/// <summary>
/// Everything collected so far in the current flow, serialized as-is into the state store.
/// One bag for all flows keeps the store's schema stable; unused fields are just null.
/// </summary>
public sealed class ConversationData
{
    // Registering / LoggingIn
    public string? NickName { get; set; }
    public string? Email { get; set; }

    // CreatingQuiz
    public string? QuizTitle { get; set; }
    public string? QuizDescription { get; set; }
    public Guid? QuizSetId { get; set; }

    // AddingQuestion (current question being built)
    public string? QuestionText { get; set; }
    public int? QuestionTypeRaw { get; set; }
    public int? QuestionTimeLimitSeconds { get; set; }
    public int? QuestionPoints { get; set; }
    public List<PendingOption> PendingOptions { get; set; } = [];

    // Join flow
    public string? RoomCode { get; set; }

    // Broadcast flow
    public string? BroadcastMessage { get; set; }
}

public sealed record PendingOption(string Text, bool IsCorrect, int OrderIndex);

public sealed class ConversationContext
{
    public ConversationFlow Flow { get; set; } = ConversationFlow.None;
    public ConversationStep Step { get; set; } = ConversationStep.None;
    public ConversationData Data { get; set; } = new();
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
