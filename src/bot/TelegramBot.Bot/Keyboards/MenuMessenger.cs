using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Application.Interfaces;

namespace TelegramBot.Bot.Keyboards;

public sealed class MenuMessenger(ITelegramBotClient botClient, IMenuMessageStore menuStore)
{
    public async Task ShowAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? keyboard,
        CancellationToken ct,
        ParseMode parseMode = ParseMode.None)
    {
        var existingMessageId = await menuStore.GetAsync(chatId, ct);

        if (existingMessageId is not null)
        {
            try
            {
                await botClient.EditMessageText(
                    chatId, existingMessageId.Value, text,
                    parseMode: parseMode, replyMarkup: keyboard, cancellationToken: ct);
                return;
            }
            catch (ApiRequestException)
            {
            }
        }

        var sent = await botClient.SendMessage(
            chatId, text, parseMode: parseMode, replyMarkup: keyboard, cancellationToken: ct);

        await menuStore.SetAsync(chatId, sent.MessageId, ct);
    }

    public Task ForgetAsync(long chatId, CancellationToken ct) => menuStore.ClearAsync(chatId, ct);
}
