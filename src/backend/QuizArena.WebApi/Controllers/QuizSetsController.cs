using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.DeleteQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.PublishQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.UnpublishQuizSet;
using QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;
using QuizArena.Application.Features.QuizSets.Queries.GetMyQuizSets;
using QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;
using QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;
using QuizArena.WebApi.Contracts.QuizSets;

namespace QuizArena.WebApi.Controllers;

public sealed class QuizSetsController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateQuizSetCommand command, CancellationToken ct = default)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetQuizSetByIdQuery(id), ct);
        return HandleResult(result);
    }
    
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMy(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetMyQuizSetsQuery(), ct);
        return HandleResult(result);
    }
    
    [HttpGet("public")]
    public async Task<IActionResult> GetPublic(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetPublicQuizSetsQuery(), ct);
        return HandleResult(result);
    }
    
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, UpdateQuizSetDetailsRequest request, CancellationToken ct = default)
    {
        var command = new UpdateQuizSetDetailsCommand(id, request.Title, request.Description);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new PublishQuizSetCommand(id), ct);
        return HandleResult(result);
    }

    [HttpPost("{id:guid}/unpublish")]
    [Authorize]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new UnpublishQuizSetCommand(id), ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteQuizSetCommand(id), ct);
        return HandleResult(result);
    }
}