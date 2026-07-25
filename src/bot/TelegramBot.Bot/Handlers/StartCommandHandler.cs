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

    public async Task HandleHelpAsync(UpdateContext context, CancellationToken ct)
    {
        var lines = new List<string>
        {
            "*Загальні команди:*",
            "/start — головне меню",
            "/help — цей список команд",
            "/join — приєднатись до гри за кодом кімнати",
        };

        if (context.IsAuthenticated)
        {
            lines.Add("");
            lines.Add("*Для власника акаунту:*");
            lines.Add("🗂 Мої квізи — список і керування власними квізами");
            lines.Add("🌐 Каталог — публічні квізи інших користувачів");
            lines.Add("/newquiz — створити новий квіз");
            lines.Add("/logout — вийти з акаунту");
        }
        else
        {
            lines.Add("");
            lines.Add("*Щоб створювати квізи, потрібен акаунт:*");
            lines.Add("🔑 Увійти — вхід в існуючий акаунт");
            lines.Add("📝 Реєстрація — створити акаунт");
        }

        if (context.IsAdmin)
        {
            lines.Add("");
            lines.Add("*Адмін\\-команди:*");
            lines.Add("/admin\\_stats — статистика бота");
            lines.Add("/admin\\_broadcast — розсилка всім користувачам");
            lines.Add("/admin\\_maintenance\\_on — увімкнути режим обслуговування");
            lines.Add("/admin\\_maintenance\\_off — вимкнути режим обслуговування");
        }

        await botClient.SendMessage(context.ChatId, string.Join('\n', lines),
            parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, cancellationToken: ct);
    }
}
