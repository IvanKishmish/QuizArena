using Mediator;
using ErrorOr;
using QuizArena.Application.Common;

namespace QuizArena.Application.Features.Admin.Queries.GetAllUsers;

public sealed record UserSummary(Guid Id, string Email, string Nickname, bool IsBanned, DateTimeOffset RegisteredAt);

public sealed record GetAllUsersQuery(int PageNumber = 1, int PageSize = 20)
: IQuery<ErrorOr<PagedResponse<UserSummary>>>, IPagedQuery;