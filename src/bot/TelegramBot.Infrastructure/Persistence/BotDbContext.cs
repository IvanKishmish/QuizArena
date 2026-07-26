using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TelegramBot.Domain.Entities;

namespace TelegramBot.Infrastructure.Persistence;

/// <summary>
/// The bot's own small PostgreSQL schema — deliberately separate database/schema from
/// the main QuizArena DB (see README ADR "Token storage"). The bot never touches the
/// backend's tables directly; it only ever talks to the backend through its HTTP API.
///
/// Implements IDataProtectionKeyContext (adds the DataProtectionKeys DbSet below) so
/// ASP.NET Core's DataProtection key ring — used to encrypt/decrypt the access and
/// refresh tokens stored in telegram_user_sessions — is persisted here instead of the
/// container's local filesystem. Without this, every container restart/redeploy would
/// generate a fresh key and silently invalidate every stored token.
/// </summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<BotUserSession> UserSessions => Set<BotUserSession>();
    public DbSet<TelegramBot.Domain.Entities.BotAdmin> BotAdmins => Set<TelegramBot.Domain.Entities.BotAdmin>();
    public DbSet<BroadcastLog> BroadcastLogs => Set<BroadcastLog>();
    public DbSet<BotUsageStat> UsageStats => Set<BotUsageStat>();
    public DbSet<BotMessageLog> MessageLogs => Set<BotMessageLog>();

    // Required by IDataProtectionKeyContext — name/type must match exactly.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<BotUserSession>(e =>
        {
            e.ToTable("telegram_user_sessions");
            e.HasKey(x => x.ChatId);
            e.Property(x => x.ChatId).ValueGeneratedNever();
            e.Property(x => x.TelegramUsername).HasMaxLength(64);
            e.Property(x => x.NickName).HasMaxLength(64);
            e.Property(x => x.AccessTokenEncrypted).HasMaxLength(4096);
            e.Property(x => x.RefreshTokenCookieEncrypted).HasMaxLength(4096);
            e.HasIndex(x => x.QuizArenaUserId);
        });

        builder.Entity<TelegramBot.Domain.Entities.BotAdmin>(e =>
        {
            e.ToTable("bot_admins");
            e.HasKey(x => x.TelegramUserId);
            e.Property(x => x.TelegramUserId).ValueGeneratedNever();
            e.Property(x => x.TelegramUsername).HasMaxLength(64);
            e.Property(x => x.GrantedBy).HasMaxLength(64);
        });

        builder.Entity<BroadcastLog>(e =>
        {
            e.ToTable("broadcast_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.MessageText).HasMaxLength(4096);
        });

        builder.Entity<BotUsageStat>(e =>
        {
            e.ToTable("bot_usage_stats");
            e.HasKey(x => x.DateUtc);
        });

        builder.Entity<BotMessageLog>(e =>
        {
            e.ToTable("bot_message_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.TelegramUsername).HasMaxLength(64);
            e.Property(x => x.NickName).HasMaxLength(64);
            e.Property(x => x.Text).HasMaxLength(4096);
            e.HasIndex(x => x.ChatId);
            e.HasIndex(x => x.SentAtUtc);
        });
    }
}
