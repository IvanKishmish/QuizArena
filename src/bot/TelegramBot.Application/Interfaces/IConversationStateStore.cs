using TelegramBot.Application.Conversations;

namespace TelegramBot.Application.Interfaces;

/// <summary>
/// Where a chat's current position in a multi-step dialog lives. Backed by Redis
/// (see README ADR) so that a bot restart doesn't strand a user mid-registration —
/// unlike tokens, this data is short-lived and fine to lose, but losing it mid-conversation
/// is a bad UX moment we can cheaply avoid since Redis is already a hard dependency
/// (it also backs the QuizArena backend's live-game state).
/// </summary>
public interface IConversationStateStore
{
    Task<ConversationContext> GetAsync(long chatId, CancellationToken ct);
    Task SetAsync(long chatId, ConversationContext context, CancellationToken ct);
    Task ClearAsync(long chatId, CancellationToken ct);
}
