using Telegram.Bot;
using TelegramBot.Application.Interfaces;
using TelegramBot.Bot.Keyboards;
using TelegramBot.Bot.Middleware;

namespace TelegramBot.Bot.Handlers;

public sealed class StartCommandHandler(ITelegramBotClient botClient, IConversationStateStore stateStore)
{
    public async Task HandleAsync(UpdateContext context, CancellationToken ct)
    {
        await stateStore.ClearAsync(context.ChatId, ct);

        var greeting = context.IsAuthenticated
            ? "З поверненням до QuizArena! 👋 Що робимо?"
            : "Привіт! 👋 Це бот QuizArena — живі квізи прямо в Telegram.\n\n" +
              "Щоб створювати квізи й запускати ігри, потрібен акаунт. Приєднатись до чужої гри можна і без нього.";

        await botClient.SendMessage(context.ChatId, greeting, replyMarkup: KeyboardFactory.MainMenu(context.IsAuthenticated), cancellationToken: ct);
    }
}
