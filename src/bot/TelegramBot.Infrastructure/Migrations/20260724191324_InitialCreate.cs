using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TelegramBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bot_admins",
                columns: table => new
                {
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_admins", x => x.TelegramUserId);
                });

            migrationBuilder.CreateTable(
                name: "bot_usage_stats",
                columns: table => new
                {
                    DateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    MessagesProcessed = table.Column<long>(type: "bigint", nullable: false),
                    UniqueChatsSeen = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_usage_stats", x => x.DateUtc);
                });

            migrationBuilder.CreateTable(
                name: "broadcast_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatedByTelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    MessageText = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broadcast_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telegram_user_sessions",
                columns: table => new
                {
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    QuizArenaUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NickName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefreshTokenCookieEncrypted = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsecutiveAuthFailures = table.Column<int>(type: "integer", nullable: false),
                    IsFlaggedAsPossiblyBanned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_user_sessions", x => x.ChatId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_user_sessions_QuizArenaUserId",
                table: "telegram_user_sessions",
                column: "QuizArenaUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_admins");

            migrationBuilder.DropTable(
                name: "bot_usage_stats");

            migrationBuilder.DropTable(
                name: "broadcast_logs");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "telegram_user_sessions");
        }
    }
}
