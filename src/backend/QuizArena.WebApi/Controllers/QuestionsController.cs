using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizArena.Application.Features.Questions.Commands.AddQuestion;
using QuizArena.Application.Features.Questions.Commands.DeleteQuestion;
using QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;
using QuizArena.WebApi.Contracts.QuizSets;

namespace QuizArena.WebApi.Controllers;

[Route("api/quizsets/{quizSetId:guid}/questions")]
public sealed class QuestionsController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add(Guid quizSetId, AddQuestionRequest request, CancellationToken ct = default)
    {
        var command = new AddQuestionCommand(
            quizSetId, request.Text, request.QuestionType,
            request.TimeLimitSeconds, request.Points, request.Options);
        var result = await Mediator.Send(command, ct);
        return HandleResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetByQuizSet(Guid quizSetId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetQuestionsByQuizSetQuery(quizSetId), ct);
        return HandleResult(result);
    }

    [HttpDelete("{questionId:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid quizSetId, Guid questionId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new DeleteQuestionCommand(quizSetId, questionId), ct);
        return HandleResult(result);
    }
}