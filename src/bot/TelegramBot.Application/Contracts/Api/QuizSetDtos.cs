namespace TelegramBot.Application.Contracts.Api;

public sealed record CreateQuizSetRequest(string Title, string Description);

public sealed record CreatedQuizSetResponse(Guid Id);

public sealed record UpdateQuizSetRequest(string Title, string Description);

public sealed record QuizSetSummaryResponse(
    Guid Id,
    string Title,
    string Description,
    bool IsPublished,
    int QuestionCount);

public sealed record QuizSetDetailsResponse(
    Guid Id,
    string Title,
    string Description,
    bool IsPublished,
    Guid OwnerId);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
