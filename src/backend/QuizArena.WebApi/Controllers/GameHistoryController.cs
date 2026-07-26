using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.GameHistory.Queries.GetMyGameHistory;

namespace QuizArena.WebApi.Controllers;

public sealed class GameHistoryController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMy(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetMyGameHistoryQuery(pageNumber, pageSize), ct);
        return HandleResult(result);
    }
}