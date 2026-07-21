using Mediator;
using Microsoft.Extensions.Logging;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.GameRooms.Events;

namespace QuizArena.Application.Features.Auth.Events;

public sealed class SendWelcomeEmailHandler(
    IEmailSender emailSender,
    ILogger<SendWelcomeEmailHandler> logger
    ) : INotificationHandler<UserRegisteredNotification>
{
    public async ValueTask Handle(UserRegisteredNotification notification, CancellationToken ct = default)
    {
        try
        {
            var html = $"""
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
                            <!-- Main Container -->
                            <table width="600" cellpadding="0" cellspacing="0" style="background-color: #1e293b; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.5);">
                                
                                <!-- Header -->
                                <tr>
                                    <td align="center" style="padding: 40px 20px; background: linear-gradient(135deg, #6366f1 0%, #a855f7 100%);">
                                        <h1 style="margin: 0; color: #ffffff; font-size: 36px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase;">
                                            QuizArena
                                        </h1>
                                    </td>
                                </tr>
                                
                                <!-- Body -->
                                <tr>
                                    <td style="padding: 40px 30px;">
                                        <h2 style="margin-top: 0; color: #a855f7; font-size: 24px; font-weight: 700;">
                                            Welcome to the arena, {notification.NickName}! 🎮
                                        </h2>
                                        
                                        <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 20px;">
                                            You have successfully joined <strong>QuizArena</strong>. Get ready to test your knowledge in real-time, compete with other players, and climb to the top of the leaderboard!
                                        </p>
                                        
                                        <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 40px;">
                                            Your account is ready. Other challengers are already warming up, so don't keep them waiting.
                                        </p>
                                        
                                        <!-- Call to Action Button -->
                                        <div style="text-align: center;">
                                            <a href="https://quizarena.com/login" style="display: inline-block; padding: 16px 36px; background-color: #6366f1; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: bold; border-radius: 8px; text-transform: uppercase; letter-spacing: 1px; box-shadow: 0 4px 15px rgba(99, 102, 241, 0.4);">
                                                Enter the Arena
                                            </a>
                                        </div>
                                    </td>
                                </tr>
                                
                                <!-- Footer -->
                                <tr>
                                    <td align="center" style="padding: 24px; background-color: #0f172a; border-top: 1px solid #334155;">
                                        <p style="margin: 0; font-size: 13px; color: #64748b; line-height: 1.5;">
                                            © 2026 QuizArena. All rights reserved.<br>
                                            If you didn't create an account, you can safely ignore this email.
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

            await emailSender.SendAsync(notification.Email, "🎮 Welcome to QuizArena!", html, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send welcome email to {Email}", notification.Email);
        }
    }
}