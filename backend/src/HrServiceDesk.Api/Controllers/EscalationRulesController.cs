using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Escalations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Escalation rules (HR Admin). Each rule fires at most once per case.</summary>
[Route("api/escalation-rules")]
[Authorize(Policy = Policies.CanAdministerTenant)]
public sealed class EscalationRulesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EscalationRuleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleDto>>> List(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListEscalationRulesQuery(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<EscalationRuleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EscalationRuleDto>> Create(SaveEscalationRuleCommand command, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(command with { Id = null }, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<EscalationRuleDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EscalationRuleDto>> Update(Guid id, SaveEscalationRuleCommand command, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(command with { Id = id }, cancellationToken));
}
