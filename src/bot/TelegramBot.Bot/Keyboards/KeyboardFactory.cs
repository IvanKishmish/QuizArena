using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Application.Contracts.Api;

namespace TelegramBot.Bot.Keyboards;

public static class KeyboardFactory
{
    public static ReplyKeyboardMarkup MainMenu(bool isAuthenticated) => new(
        isAuthenticated
            ? new[]
            {
                new[] { new KeyboardButton("🗂 Мої квізи"), new KeyboardButton("🌐 Каталог") },
                new[] { new KeyboardButton("🎮 Приєднатись до гри"), new KeyboardButton("📜 Історія ігор") },
                new[] { new KeyboardButton("🚪 Вийти") },
            }
            : new[]
            {
                new[] { new KeyboardButton("🔑 Увійти"), new KeyboardButton("📝 Реєстрація") },
                new[] { new KeyboardButton("🎮 Приєднатись до гри") },
            })
    { ResizeKeyboard = true };

    public static InlineKeyboardMarkup MyQuizzesList(IReadOnlyList<(Guid Id, string Title, bool Published)> quizzes)
    {
        var rows = quizzes
            .Select(q => new[] { InlineKeyboardButton.WithCallbackData($"{(q.Published ? "🟢" : "⚪")} {q.Title}", $"quiz:open:{q.Id}") })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Новий квіз", "quiz:create") });

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup PublicCatalog(IReadOnlyList<(Guid Id, string Title)> quizzes, int page, int totalPages)
    {
        var rows = quizzes
            .Select(q => new[] { InlineKeyboardButton.WithCallbackData(q.Title, $"catalog:open:{q.Id}") })
            .ToList();

        var nav = new List<InlineKeyboardButton>();
        if (page > 1) nav.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"catalog:page:{page - 1}"));
        if (page < totalPages) nav.Add(InlineKeyboardButton.WithCallbackData("Далі ➡️", $"catalog:page:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup QuizDetailsActions(Guid quizId, bool isPublished) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ Додати питання", $"quiz:addq:{quizId}") },
        new[] { InlineKeyboardButton.WithCallbackData("📋 Питання", $"quiz:questions:{quizId}") },
        new[] { InlineKeyboardButton.WithCallbackData("✏️ Редагувати назву/опис", $"quiz:edit:{quizId}") },
        new[]
        {
            isPublished
                ? InlineKeyboardButton.WithCallbackData("🔒 Зняти з публікації", $"quiz:unpublish:{quizId}")
                : InlineKeyboardButton.WithCallbackData("📢 Опублікувати", $"quiz:publish:{quizId}")
        },
        new[] { InlineKeyboardButton.WithCallbackData("🎮 Створити ігрову кімнату", $"room:create:{quizId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Видалити квіз", $"quiz:delete:{quizId}") },
    });

    public static InlineKeyboardMarkup QuestionsList(Guid quizId, IReadOnlyList<(Guid Id, string Text)> questions)
    {
        var rows = questions
            .Select(q => new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    q.Text.Length > 40 ? q.Text[..40] + "…" : q.Text, $"q:noop:{q.Id}"),
                InlineKeyboardButton.WithCallbackData("🗑", $"q:delete:{quizId}:{q.Id}")
            })
            .ToList();

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад до квізу", $"quiz:open:{quizId}") });

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup ConfirmDeleteQuestion(Guid quizId, Guid questionId) =>
        YesNo($"q:delete_confirm:{quizId}:{questionId}", $"q:delete_cancel:{quizId}");

    public static InlineKeyboardMarkup AdminUsersList(IReadOnlyList<AdminUserSummary> users, int page, int totalPages)
    {
        var rows = users
            .Select(u => new[]
            {
                u.IsBanned
                    ? InlineKeyboardButton.WithCallbackData($"✅ Розбанити {Truncate(u.Nickname, 24)}", $"admin:unban:{u.Id}:{page}")
                    : InlineKeyboardButton.WithCallbackData($"🚫 Забанити {Truncate(u.Nickname, 24)}", $"admin:ban:{u.Id}:{page}")
            })
            .ToList();

        AddPaginationRow(rows, "admin:users:", page, totalPages);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup AdminQuizSetsList(IReadOnlyList<AdminQuizSetSummary> quizzes, int page, int totalPages)
    {
        var rows = quizzes
            .Select(q => new[]
            {
                InlineKeyboardButton.WithCallbackData($"🗑 {Truncate(q.Title, 30)}", $"admin:delquiz:{q.Id}:{page}")
            })
            .ToList();

        AddPaginationRow(rows, "admin:quizsets:", page, totalPages);
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup GameHistoryList(int page, int totalPages)
    {
        var rows = new List<InlineKeyboardButton[]>();
        AddPaginationRow(rows, "history:", page, totalPages);
        return new InlineKeyboardMarkup(rows);
    }

    private static void AddPaginationRow(List<InlineKeyboardButton[]> rows, string callbackPrefix, int page, int totalPages)
    {
        var nav = new List<InlineKeyboardButton>();
        if (page > 1) nav.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"{callbackPrefix}{page - 1}"));
        if (page < totalPages) nav.Add(InlineKeyboardButton.WithCallbackData("➡️", $"{callbackPrefix}{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "…" : text;

    public static InlineKeyboardMarkup YesNo(string yesCallback, string noCallback) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Так", yesCallback),
            InlineKeyboardButton.WithCallbackData("❌ Ні", noCallback),
        }
    });

    public static InlineKeyboardMarkup QuestionTypeChoice() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("Один варіант", "qtype:0"), InlineKeyboardButton.WithCallbackData("Кілька варіантів", "qtype:1") },
        new[] { InlineKeyboardButton.WithCallbackData("Так/Ні", "qtype:2"), InlineKeyboardButton.WithCallbackData("Впорядкування", "qtype:3") },
    });

    /// <summary>Answer options for a live question — index in the callback data is what
    /// SubmitAnswer's selectedOptionIndices expects, never the option's own id.</summary>
    public static InlineKeyboardMarkup AnswerOptions(IReadOnlyList<QuestionStartedOption> options, bool multipleChoice)
    {
        var rows = options
            .Select(o => new[] { InlineKeyboardButton.WithCallbackData(o.Text, $"answer:{o.Index}") })
            .ToList();

        if (multipleChoice)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Підтвердити вибір", "answer:submit") });

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup PowerUps(IReadOnlyList<PowerUpType> available) => new(
        available.Select(p => new[]
        {
            InlineKeyboardButton.WithCallbackData(PowerUpLabel(p), $"powerup:{(int)p}")
        }));

    private static string PowerUpLabel(PowerUpType type) => type switch
    {
        PowerUpType.Freeze => "🧊 Freeze",
        PowerUpType.FiftyFifty => "➗ 50/50",
        PowerUpType.DoubleOrNothing => "🎲 Double or Nothing",
        _ => type.ToString()
    };

    public static InlineKeyboardMarkup HostControls() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("▶️ Наступне питання", "host:next") },
        new[] { InlineKeyboardButton.WithCallbackData("🏁 Завершити гру", "host:end") },
    });

    public static ReplyKeyboardRemove Remove() => new();
}
