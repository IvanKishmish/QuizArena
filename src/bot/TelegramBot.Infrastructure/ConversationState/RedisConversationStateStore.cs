using System.Text.Json;
using StackExchange.Redis;
using TelegramBot.Application.Conversations;
using TelegramBot.Application.Interfaces;

namespace TelegramBot.Infrastructure.ConversationState;

/// <summary>
/// FSM position for a chat, stored in the same Redis instance the QuizArena backend
/// already runs for live-game state (see docker-compose). A 30-minute sliding TTL means
/// an abandoned "creating a quiz" dialog just quietly expires instead of accumulating
/// forever; the chat simply starts fresh next time.
/// </summary>
public sealed class RedisConversationStateStore(IConnectionMultiplexer redis) : IConversationStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();
    private static string Key(long chatId) => $"bot:conversation:{chatId}";

    public async Task<ConversationContext> GetAsync(long chatId, CancellationToken ct)
    {
        var raw = await Db.StringGetAsync(Key(chatId));
        if (raw.IsNullOrEmpty)
            return new ConversationContext();

        return JsonSerializer.Deserialize<ConversationContext>((string)raw!, JsonOptions) ?? new ConversationContext();
    }

    public async Task SetAsync(long chatId, ConversationContext context, CancellationToken ct)
    {
        context.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(context, JsonOptions);
        await Db.StringSetAsync(Key(chatId), json, Ttl);
    }

    public async Task ClearAsync(long chatId, CancellationToken ct)
    {
        await Db.KeyDeleteAsync(Key(chatId));
    }
}
