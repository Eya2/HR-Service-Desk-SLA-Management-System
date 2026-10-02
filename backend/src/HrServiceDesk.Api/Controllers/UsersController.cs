using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Users;
using HrServiceDesk.Application.Users.Commands;
using HrServiceDesk.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>User administration for the caller's organisation (HR Admin).</summary>
[Route("api/users")]
[Authorize(Policy = Policies.CanAdministerTenant)]
public sealed class UsersController : ApiControllerBase
{
    public sealed record UpdateUserRequest(string FirstName, string LastName, IReadOnlyList<string> Roles, Guid? ManagerId, bool IsActive);

    public sealed record ResetPasswordRequest(string NewPassword);

    /// <summary>Lists users, with optional search on name or e-mail and a role filter.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> List(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await Sender.Send(new ListUsersQuery(search, role, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailsDto>> Get(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new GetUserQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDetailsDto>> Create(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : Problem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDetailsDto>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(
            new UpdateUserCommand(id, request.FirstName, request.LastName, request.Roles, request.ManagerId, request.IsActive),
            cancellationToken));

    /// <summary>Sets a new password for the user and signs them out everywhere.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new ResetUserPasswordCommand(id, request.NewPassword), cancellationToken));
}
