using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using TelegramBot.Application.Interfaces;
using TelegramBot.Domain.Entities;
using TelegramBot.Infrastructure.Persistence;
using TelegramBot.Infrastructure.Realtime;

namespace TelegramBot.Infrastructure.BotAdmin;

/// <summary>
/// "Unique users ever" and "messages today" come from the bot's own DB/Redis; "active
/// game sessions now" is read straight off the live SignalR connection manager, since
/// that's the actual source of truth (no separate counter to let drift out of sync).
/// </summary>
public sealed class BotStatsService(BotDbContext db, IConnectionMultiplexer redis, GameSignalRConnectionManager connectionManager)
    : IBotStatsService
{
    private IDatabase Redis => redis.GetDatabase();
    private static string DailyCounterKey(DateOnly date) => $"bot:stats:messages:{date:yyyyMMdd}";

    public async Task RecordMessageProcessedAsync(long chatId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Redis.StringIncrementAsync(DailyCounterKey(today));
        await Redis.KeyExpireAsync(DailyCounterKey(today), TimeSpan.FromDays(2));
    }

    public async Task<BotStatsSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var totalUsers = await db.UserSessions.CountAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var messagesTodayRaw = await Redis.StringGetAsync(DailyCounterKey(today));
        var messagesToday = messagesTodayRaw.IsNullOrEmpty ? 0 : (long)messagesTodayRaw;

        return new BotStatsSnapshot(totalUsers, connectionManager.ActiveConnectionCount, messagesToday);
    }
}

public sealed class BroadcastService(BotDbContext db, ITelegramBotClient botClient) : IBroadcastService
{
    public Task<int> GetRecipientCountAsync(CancellationToken ct) =>
        db.UserSessions.CountAsync(ct);

    public async Task<(int success, int failure)> BroadcastAsync(long initiatedByTelegramUserId, string text, CancellationToken ct)
    {
        var log = new BroadcastLog
        {
            Id = Guid.NewGuid(),
            InitiatedByTelegramUserId = initiatedByTelegramUserId,
            MessageText = text,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        var chatIds = await db.UserSessions.AsNoTracking().Select(x => x.ChatId).ToListAsync(ct);
        log.TargetCount = chatIds.Count;

        int success = 0, failure = 0;
        foreach (var chatId in chatIds)
        {
            try
            {
                await botClient.SendMessage(chatId, text, cancellationToken: ct);
                success++;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 403)
            {
                // User blocked the bot — expected and not worth logging loudly per-occurrence.
                failure++;
            }
            catch
            {
                failure++;
            }

            // Telegram's Bot API rate limit is ~30 msg/sec across the bot; a small delay keeps
            // a large broadcast from tripping 429s instead of relying on Polly to paper over it.
            await Task.Delay(35, ct);
        }

        log.SuccessCount = success;
        log.FailureCount = failure;
        log.CompletedAtUtc = DateTimeOffset.UtcNow;

        db.BroadcastLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return (success, failure);
    }
}
