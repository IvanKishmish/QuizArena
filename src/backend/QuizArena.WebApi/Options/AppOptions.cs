namespace QuizArena.WebApi.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string BaseUrl { get; init; } = "https://quizarena.com";
}
