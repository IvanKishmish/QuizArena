using StackExchange.Redis;
using TelegramBot.Application.Interfaces;

namespace TelegramBot.Infrastructure.ConversationState;

public sealed class RedisMenuMessageStore(IConnectionMultiplexer redis) : IMenuMessageStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(6);

    private IDatabase Db => redis.GetDatabase();
    private static string Key(long chatId) => $"bot:menu-message:{chatId}";

    public async Task<int?> GetAsync(long chatId, CancellationToken ct)
    {
        var raw = await Db.StringGetAsync(Key(chatId));
        return raw.IsNullOrEmpty ? null : (int)raw;
    }

    public async Task SetAsync(long chatId, int messageId, CancellationToken ct)
    {
        await Db.StringSetAsync(Key(chatId), messageId, Ttl);
    }

    public async Task ClearAsync(long chatId, CancellationToken ct)
    {
        await Db.KeyDeleteAsync(Key(chatId));
    }
}
