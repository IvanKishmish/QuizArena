using TelegramBot.Application.Common;
using TelegramBot.Application.Contracts.Api;

namespace TelegramBot.Application.Interfaces;

/// <summary>
/// Typed façade over the QuizArena REST API for a single Telegram chat.
/// Every call is scoped to one chatId: the implementation looks up that chat's
/// stored tokens, attaches Authorization, retries transient failures via Polly,
/// and transparently refreshes an expired access token once before giving up.
/// No handler in the Bot layer should ever touch HttpClient directly.
/// </summary>
public interface IQuizArenaApiClient
{
    // ---- Auth (no chatId scoping needed yet, chatId is the caller's own binding key) ----
    Task<ApiResult<TokenPairResult>> RegisterAsync(long chatId, RegisterRequest request, CancellationToken ct);
    Task<ApiResult<TokenPairResult>> LoginAsync(long chatId, LoginRequest request, CancellationToken ct);

    /// <summary>Forces a refresh using the stored cookie value. Normally called internally,
    /// but exposed for the /start "am I still logged in" check.</summary>
    Task<ApiResult<TokenPairResult>> RefreshAsync(long chatId, CancellationToken ct);

    // ---- QuizSets ----
    Task<ApiResult<CreatedQuizSetResponse>> CreateQuizSetAsync(long chatId, CreateQuizSetRequest request, CancellationToken ct);
    Task<ApiResult<QuizSetDetailsResponse>> GetQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct);
    Task<ApiResult<object?>> UpdateQuizSetAsync(long chatId, Guid quizSetId, UpdateQuizSetRequest request, CancellationToken ct);
    Task<ApiResult<object?>> DeleteQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct);
    Task<ApiResult<IReadOnlyList<QuizSetSummaryResponse>>> GetMyQuizSetsAsync(long chatId, CancellationToken ct);
    Task<ApiResult<PagedResponse<QuizSetSummaryResponse>>> GetPublicQuizSetsAsync(long chatId, int pageNumber, int pageSize, CancellationToken ct);
    Task<ApiResult<object?>> PublishQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct);
    Task<ApiResult<object?>> UnpublishQuizSetAsync(long chatId, Guid quizSetId, CancellationToken ct);

    // ---- Questions ----
    Task<ApiResult<object?>> AddQuestionAsync(long chatId, Guid quizSetId, AddQuestionRequest request, CancellationToken ct);
    Task<ApiResult<IReadOnlyList<QuestionResponse>>> GetQuestionsAsync(long chatId, Guid quizSetId, CancellationToken ct);
    Task<ApiResult<object?>> DeleteQuestionAsync(long chatId, Guid quizSetId, Guid questionId, CancellationToken ct);

    // ---- Game rooms (REST part; live play goes through IGameLiveConnectionManager) ----
    Task<ApiResult<CreateGameRoomResponse>> CreateGameRoomAsync(long chatId, CreateGameRoomRequest request, CancellationToken ct);
    Task<ApiResult<JoinGameRoomResponse>> JoinGameRoomAsync(long chatId, string roomCode, JoinGameRoomRequest request, CancellationToken ct);
    Task<ApiResult<object?>> StartGameAsync(long chatId, string roomCode, CancellationToken ct);
}
