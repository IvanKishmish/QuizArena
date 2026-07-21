using Mediator;
using Microsoft.Extensions.Logging;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Common.Interfaces.Leaderboard;

namespace QuizArena.Application.Features.GameRooms.Events;

public sealed class SendGameResultsEmailHandler(
    IIdentityService identityService,
    IEmailSender emailSender,
    ILogger<SendGameResultsEmailHandler> logger
) : INotificationHandler<GameFinishedNotification>
{
    public async ValueTask Handle(GameFinishedNotification notification, CancellationToken ct = default)
    {
        var userIds = notification.ParticipantUserIds.Values
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return;
        
        var emailsByUserId = await identityService.GetEmailsAsync(userIds, ct);

        var recipients = new List<(string Email, LeaderboardEntry Entry, int Placement)>();

        for (int i = 0; i < notification.FinalLeaderboard.Count; i++)
        {
            var entry = notification.FinalLeaderboard[i];

            if (!notification.ParticipantUserIds.TryGetValue(entry.ParticipantId, out var userId) || userId is null)
                continue;
            
            if(!emailsByUserId.TryGetValue(userId.Value, out var email) || string.IsNullOrWhiteSpace(email))
                continue;
            
            recipients.Add((email, entry, i + 1));
        }
        
        if (recipients.Count == 0)
            return;

        await Task.WhenAll(recipients.Select(r => SendResultEmailAsync(r.Email, r.Entry, r.Placement, ct)));
    }
    
    private async Task SendResultEmailAsync(string email, LeaderboardEntry entry, int placement, CancellationToken ct = default)
    {
        try
        {
            var isWinner = placement == 1;
            var subject = isWinner
                ? "🏆 You won QuizArena!"
                : $"Your QuizArena results — #{placement} place";

            await emailSender.SendAsync(email, subject, BuildHtml(entry, placement, isWinner), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send game results email to {Email}", email);
        }
    }

    private static string BuildHtml(LeaderboardEntry entry, int placement, bool isWinner)
    {
        var headline = isWinner
            ? "You took first place! 🏆"
            : $"You finished #{placement}";

        var accent = isWinner ? "#facc15" : "#a855f7";

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body style="margin: 0; padding: 0; background-color: #0f172a; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #f8fafc;">
            <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #0f172a; padding: 40px 0;">
                <tr>
                    <td align="center">
                        <table width="600" cellpadding="0" cellspacing="0" style="background-color: #1e293b; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.5);">

                            <tr>
                                <td align="center" style="padding: 40px 20px; background: linear-gradient(135deg, #6366f1 0%, #a855f7 100%);">
                                    <h1 style="margin: 0; color: #ffffff; font-size: 36px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase;">
                                        QuizArena
                                    </h1>
                                </td>
                            </tr>

                            <tr>
                                <td style="padding: 40px 30px;">
                                    <h2 style="margin-top: 0; color: {accent}; font-size: 24px; font-weight: 700;">
                                        {headline}
                                    </h2>

                                    <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 8px;">
                                        Hey {entry.DisplayName}, the game has ended.
                                    </p>

                                    <table width="100%" cellpadding="0" cellspacing="0" style="margin: 24px 0; background-color: #0f172a; border-radius: 12px;">
                                        <tr>
                                            <td style="padding: 20px; text-align: center;">
                                                <div style="font-size: 13px; color: #64748b; text-transform: uppercase; letter-spacing: 1px;">Final Score</div>
                                                <div style="font-size: 32px; font-weight: 800; color: {accent};">{entry.Score:0}</div>
                                            </td>
                                        </tr>
                                    </table>

                                    <div style="text-align: center;">
                                        <a href="https://quizarena.com/login" style="display: inline-block; padding: 16px 36px; background-color: #6366f1; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: bold; border-radius: 8px; text-transform: uppercase; letter-spacing: 1px; box-shadow: 0 4px 15px rgba(99, 102, 241, 0.4);">
                                            Play Again
                                        </a>
                                    </div>
                                </td>
                            </tr>

                            <tr>
                                <td align="center" style="padding: 24px; background-color: #0f172a; border-top: 1px solid #334155;">
                                    <p style="margin: 0; font-size: 13px; color: #64748b; line-height: 1.5;">
                                        © 2026 QuizArena. All rights reserved.
                                    </p>
                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }
}