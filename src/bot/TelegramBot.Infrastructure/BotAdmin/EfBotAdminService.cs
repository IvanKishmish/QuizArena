using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TelegramBot.Application.Interfaces;
using TelegramBot.Domain.Entities;
using TelegramBot.Infrastructure.Persistence;

namespace TelegramBot.Infrastructure.BotAdmin;

/// <summary>Bot's own admin list — a table in the bot's own DB, unrelated to the
/// backend's Identity "Admin" role/claim (Part 4 requirement).</summary>
public sealed class EfBotAdminService(BotDbContext db) : IBotAdminService
{
    public Task<bool> IsAdminAsync(long telegramUserId, CancellationToken ct) =>
        db.BotAdmins.AsNoTracking().AnyAsync(x => x.TelegramUserId == telegramUserId, ct);

    public async Task GrantAsync(long telegramUserId, string? username, string grantedBy, CancellationToken ct)
    {
        if (await db.BotAdmins.AnyAsync(x => x.TelegramUserId == telegramUserId, ct))
            return;

        db.BotAdmins.Add(new Domain.Entities.BotAdmin
        {
            TelegramUserId = telegramUserId,
            TelegramUsername = username,
            GrantedAtUtc = DateTimeOffset.UtcNow,
            GrantedBy = grantedBy
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(long telegramUserId, CancellationToken ct)
    {
        var admin = await db.BotAdmins.FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId, ct);
        if (admin is null) return;
        db.BotAdmins.Remove(admin);
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<long>> GetAllAdminIdsAsync(CancellationToken ct) =>
        db.BotAdmins.AsNoTracking().Select(x => x.TelegramUserId).ToListAsync(ct)
          .ContinueWith(t => (IReadOnlyList<long>)t.Result, ct);
}

/// <summary>
/// A single Redis flag rather than a DB row: maintenance mode needs to be checked on
/// *every* incoming update (see MaintenanceModeMiddleware), so it has to be a
/// sub-millisecond read. Redis is already a hard dependency for FSM state and the
/// backend's live-game state, so this adds no new infrastructure.
/// </summary>
public sealed class RedisMaintenanceModeService(IConnectionMultiplexer redis) : IMaintenanceModeService
{
    private const string Key = "bot:maintenance_mode";
    private IDatabase Db => redis.GetDatabase();

    public async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        var value = await Db.StringGetAsync(Key);
        return value == "1";
    }

    public async Task SetAsync(bool enabled, CancellationToken ct)
    {
        await Db.StringSetAsync(Key, enabled ? "1" : "0");
    }
}
