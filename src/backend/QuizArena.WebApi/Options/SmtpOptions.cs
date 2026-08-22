using System.ComponentModel.DataAnnotations;

namespace QuizArena.WebApi.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = string.Empty;

    public string? FromAddress { get; init; }

    public string FromName { get; init; } = "QuizArena";
}
