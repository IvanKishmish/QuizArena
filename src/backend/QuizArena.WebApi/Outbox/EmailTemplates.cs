namespace QuizArena.WebApi.Outbox;

internal static class EmailTemplates
{
    public static string Welcome(string baseUrl, string nickName) => Layout(
        accent: "#a855f7",
        heading: $"Welcome to the arena, {Encode(nickName)}! 🎮",
        bodyHtml: """
                  <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 20px;">
                      You have successfully joined <strong>QuizArena</strong>. Get ready to test your knowledge in real-time, compete with other players, and climb to the top of the leaderboard!
                  </p>

                  <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 40px;">
                      Your account is ready. Other challengers are already warming up, so don't keep them waiting.
                  </p>
                  """,
        ctaText: "Enter the Arena",
        ctaUrl: $"{baseUrl}/login");

    public static string GameResults(string baseUrl, string displayName, int score, int placement, bool isWinner)
    {
        var headline = isWinner ? "You took first place! 🏆" : $"You finished #{placement}";
        var accent = isWinner ? "#facc15" : "#a855f7";

        var bodyHtml = $"""
                        <p style="font-size: 16px; line-height: 1.6; color: #cbd5e1; margin-bottom: 8px;">
                            Hey {Encode(displayName)}, the game has ended.
                        </p>

                        <table width="100%" cellpadding="0" cellspacing="0" style="margin: 24px 0; background-color: #0f172a; border-radius: 12px;">
                            <tr>
                                <td style="padding: 20px; text-align: center;">
                                    <div style="font-size: 13px; color: #64748b; text-transform: uppercase; letter-spacing: 1px;">Final Score</div>
                                    <div style="font-size: 32px; font-weight: 800; color: {accent};">{score}</div>
                                </td>
                            </tr>
                        </table>
                        """;

        return Layout(accent, headline, bodyHtml, "Play Again", $"{baseUrl}/login");
    }

    public static string GameResultsSubject(int placement, bool isWinner) => isWinner
        ? "🏆 You won QuizArena!"
        : $"Your QuizArena results — #{placement} place";

    private static string Layout(string accent, string heading, string bodyHtml, string ctaText, string ctaUrl) =>
        $"""
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
                                        {heading}
                                    </h2>

                                    {bodyHtml}

                                    <div style="text-align: center;">
                                        <a href="{ctaUrl}" style="display: inline-block; padding: 16px 36px; background-color: #6366f1; color: #ffffff; text-decoration: none; font-size: 16px; font-weight: bold; border-radius: 8px; text-transform: uppercase; letter-spacing: 1px; box-shadow: 0 4px 15px rgba(99, 102, 241, 0.4);">
                                            {ctaText}
                                        </a>
                                    </div>
                                </td>
                            </tr>

                            <tr>
                                <td align="center" style="padding: 24px; background-color: #0f172a; border-top: 1px solid #334155;">
                                    <p style="margin: 0; font-size: 13px; color: #64748b; line-height: 1.5;">
                                        © {DateTimeOffset.UtcNow.Year} QuizArena. All rights reserved.
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

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
