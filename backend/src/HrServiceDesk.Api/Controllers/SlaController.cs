using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Sla;
using HrServiceDesk.Application.Sla.Commands;
using HrServiceDesk.Application.Sla.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Business calendar and SLA policies. HR staff read them; HR Admins change them.</summary>
[Authorize(Policy = Policies.CanWorkTickets)]
public sealed class SlaController : ApiControllerBase
{
    public sealed record HolidayRequest(DateOnly Date, string Name);

    public sealed record SetPolicyRequest(Guid? PolicyId);

    /// <summary>The organisation's business calendar: working hours and public holidays.</summary>
    [HttpGet("api/calendar")]
    [ProducesResponseType<CalendarDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarDto>> GetCalendar(CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetCalendarQuery(), cancellationToken));

    [HttpPut("api/calendar")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<CalendarDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CalendarDto>> UpdateCalendar(UpdateCalendarCommand command, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(command, cancellationToken));

    [HttpPost("api/calendar/holidays")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<CalendarDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarDto>> AddHoliday(HolidayRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ChangeHolidayCommand(request.Date, request.Name, Remove: false), cancellationToken));

    [HttpDelete("api/calendar/holidays/{date}")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<CalendarDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarDto>> RemoveHoliday(DateOnly date, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ChangeHolidayCommand(date, null, Remove: true), cancellationToken));

    /// <summary>The deadline <paramref name="minutes"/> business minutes after <paramref name="start"/> (to check a calendar).</summary>
    [HttpGet("api/calendar/deadline")]
    [ProducesResponseType<DateTimeOffset>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DateTimeOffset>> Deadline([FromQuery] DateTimeOffset start, [FromQuery] int minutes, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new PreviewDeadlineQuery(start, minutes), cancellationToken));

    [HttpGet("api/sla-policies")]
    [ProducesResponseType<IReadOnlyList<SlaPolicyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SlaPolicyDto>>> ListPolicies(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListSlaPoliciesQuery(), cancellationToken));

    [HttpPost("api/sla-policies")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<SlaPolicyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SlaPolicyDto>> CreatePolicy(SaveSlaPolicyCommand command, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(command with { Id = null }, cancellationToken));

    [HttpPut("api/sla-policies/{id:guid}")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType<SlaPolicyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SlaPolicyDto>> UpdatePolicy(Guid id, SaveSlaPolicyCommand command, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Chooses a request type's SLA policy (null: the default policy).</summary>
    [HttpPut("api/request-types/{id:guid}/sla-policy")]
    [Authorize(Policy = Policies.CanAdministerTenant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetRequestTypePolicy(Guid id, SetPolicyRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SetRequestTypeSlaPolicyCommand(id, request.PolicyId), cancellationToken));
}
