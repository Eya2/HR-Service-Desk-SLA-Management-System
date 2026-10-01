using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.ErrorHandling;

/// <summary>Base controller: dispatches to MediatR and maps <see cref="Result"/> failures to ProblemDetails.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected ActionResult<T> FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Problem(result.Error!);

    protected ActionResult FromResult(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error!);

    protected ObjectResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: ToStatusCode(error.Type),
            title: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = problem.Status };
    }

    internal static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.DomainRule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
}
