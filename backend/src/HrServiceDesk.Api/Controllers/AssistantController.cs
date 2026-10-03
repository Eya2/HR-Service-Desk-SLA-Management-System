using HrServiceDesk.Api.Auth;
using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Assistant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HrServiceDesk.Api.Controllers;

/// <summary>
/// The request assistant: Claude when an API key is configured, otherwise a local bilingual classifier.
/// Confidential and sensitive matters are never sent to the model.
/// </summary>
[Route("api/assistant")]
[EnableRateLimiting(AuthSetup.AiRateLimitPolicy)]
public sealed class AssistantController : ApiControllerBase
{
    public sealed record ClassifyRequest(string Text);

    /// <summary>Routes the employee's own words to a request type, with a title, pre-filled answers and help articles.</summary>
    [HttpPost("classify")]
    [ProducesResponseType<ClassificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ClassificationDto>> Classify(ClassifyRequest request, CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ClassifyRequestQuery(request.Text ?? string.Empty), cancellationToken));
}
