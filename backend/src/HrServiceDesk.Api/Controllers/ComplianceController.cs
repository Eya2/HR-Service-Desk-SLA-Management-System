using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Gdpr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Audit log (HR Admins, auditors) and data retention (HR Admins).</summary>
public sealed class ComplianceController : ApiControllerBase
{
    public sealed record RetentionRequest(int RetentionMonths);

    public sealed record RetentionRunResult(int Anonymized);

    /// <summary>Who viewed or changed sensitive data, newest first.</summary>
    [HttpGet("api/audit-logs")]
    [Authorize(Policy = Policies.CanReadAudit)]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> AuditLogs(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? action, [FromQuery] Guid? userId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListAuditLogsQuery(from, to, action, userId, page, pageSize), cancellationToken));

    [HttpGet("api/settings/retention")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<RetentionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RetentionDto>> GetRetention(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new GetRetentionQuery(), cancellationToken));

    /// <summary>Closed cases older than this many months (6–120) are anonymized by the daily job.</summary>
    [HttpPut("api/settings/retention")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<RetentionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RetentionDto>> SetRetention(RetentionRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SetRetentionCommand(request.RetentionMonths), cancellationToken));

    /// <summary>Runs the retention job now for the caller's organisation.</summary>
    [HttpPost("api/settings/retention/run")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<RetentionRunResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RetentionRunResult>> RunRetention(
        [FromServices] HrServiceDesk.Application.Abstractions.ITenantContext tenant, CancellationToken cancellationToken) =>
        Ok(new RetentionRunResult(await Sender.Send(new RunRetentionCommand(tenant.TenantId), cancellationToken)));
}
