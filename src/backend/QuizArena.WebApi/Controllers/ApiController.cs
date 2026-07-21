using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QuizArena.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiController(IMediator mediator) : Controller
{
    protected readonly IMediator Mediator = mediator;

    protected IActionResult HandleResult<T>(ErrorOr<T> result)
        => result.Match(
            value => Ok(value),
            errors => Problem(errors));
    
    protected IActionResult HandleResult(ErrorOr<Updated> result)
        => result.Match(
            _ => NoContent(),
            errors => Problem(errors));

    protected IActionResult HandleResult(ErrorOr<Deleted> result)
        => result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    
    private IActionResult Problem(List<Error> errors)
    {
        if (errors.Count == 0)
            return Problem();

        if (errors.All(e => e.Type == ErrorType.Validation))
            return ValidationProblem(errors);

        return Problem(errors[0]);
    }

    private IActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        // Per RFC 7807: "title" is a short, generic summary of the error type;
        // "detail" is the specific reason for this occurrence (error.Description).
        return Problem(
            statusCode: statusCode,
            title: GetTitleForErrorType(error.Type),
            detail: error.Description,
            type: error.Code);
    }

    private IActionResult ValidationProblem(List<Error> errors)
    {
        var modelState = new ModelStateDictionary();

        foreach (var error in errors)
            modelState.AddModelError(error.Code, error.Description);

        return ValidationProblem(modelState);
    }

    private static string GetTitleForErrorType(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Validation Error",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Resource Not Found",
        ErrorType.Conflict => "Conflict Occurred",
        _ => "An unexpected error occurred"
    };
}