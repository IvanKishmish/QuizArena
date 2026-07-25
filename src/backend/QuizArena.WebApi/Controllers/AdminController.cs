using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.Admin.Commands.BanUser;
using QuizArena.Application.Features.Admin.Commands.DeleteAnyQuizSet;
using QuizArena.Application.Features.Admin.Commands.UnbanUser;
using QuizArena.Application.Features.Admin.Queries.GetAllQuizSetsForModeration;
using QuizArena.Application.Features.Admin.Queries.GetAllUsers;
using QuizArena.Application.Features.Admin.Queries.GetDashboardStats;

namespace QuizArena.WebApi.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAllUsersQuery(pageNumber, pageSize), ct);
        return HandleResult(result);
    }

    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid userId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new BanUserCommand(userId), ct);
        return HandleResult(result);
    }

    [HttpPost("users/{userId:guid}/unban")]
    public async Task<IActionResult> UnbanUser(Guid userId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UnbanUserCommand(userId), ct);
        return HandleResult(result);
    }

    [HttpGet("quizsets")]
    public async Task<IActionResult> GetAllQuizSets(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetAllQuizSetsForModerationQuery(pageNumber, pageSize), ct);
        return HandleResult(result);
    }

    [HttpDelete("quizsets/{quizSetId:guid}")]
    public async Task<IActionResult> DeleteAnyQuizSet(Guid quizSetId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteAnyQuizSetCommand(quizSetId), ct);
        return HandleResult(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetDashboardStatsQuery(), ct);
        return HandleResult(result);
    }
}