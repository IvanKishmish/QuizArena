using System.Text.Json;
using Microsoft.Extensions.Options;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.Auth.Events;
using QuizArena.Application.Features.GameHistory.Events;
using QuizArena.WebApi.Options;

namespace QuizArena.WebApi.Outbox;

public sealed class OutboxMessageDispatcher(IEmailSender emailSender, IOptions<AppOptions> appOptions)
{
    private static readonly string WelcomeEmailType = typeof(WelcomeEmailMessage).FullName!;
    private static readonly string GameResultsEmailType = typeof(GameResultsEmailMessage).FullName!;

    public async Task DispatchAsync(string type, string payload, CancellationToken ct)
    {
        var baseUrl = appOptions.Value.BaseUrl;

        if (type == WelcomeEmailType)
        {
            var message = Deserialize<WelcomeEmailMessage>(payload);

            await emailSender.SendAsync(
                message.Email,
                "🎮 Welcome to QuizArena!",
                EmailTemplates.Welcome(baseUrl, message.NickName),
                ct);

            return;
        }

        if (type == GameResultsEmailType)
        {
            var message = Deserialize<GameResultsEmailMessage>(payload);

            await emailSender.SendAsync(
                message.Email,
                EmailTemplates.GameResultsSubject(message.Placement, message.IsWinner),
                EmailTemplates.GameResults(baseUrl, message.DisplayName, message.Score, message.Placement, message.IsWinner),
                ct);

            return;
        }

        throw new InvalidOperationException($"No outbox handler registered for message type '{type}'.");
    }

    private static TMessage Deserialize<TMessage>(string payload) =>
        JsonSerializer.Deserialize<TMessage>(payload)
        ?? throw new InvalidOperationException($"Outbox payload for '{typeof(TMessage).Name}' deserialized to null.");
}
