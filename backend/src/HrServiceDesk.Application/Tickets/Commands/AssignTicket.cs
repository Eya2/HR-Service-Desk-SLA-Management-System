using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets.Assignment;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>An agent takes a case. If two agents take it at once, the second gets a 409 conflict.</summary>
public sealed record ClaimTicketCommand(Guid TicketId) : IRequest<Result>;

/// <summary>HR staff assign a case to an agent, or release it to the team queue (null).</summary>
public sealed record AssignTicketCommand(Guid TicketId, Guid? AssigneeId) : IRequest<Result>;

/// <summary>HR staff hand a case to another team, which assigns it with its own strategy.</summary>
public sealed record MoveTicketToTeamCommand(Guid TicketId, Guid TeamId) : IRequest<Result>;

internal sealed class AssignmentHandlers(IAppDbContext db, ICurrentUser currentUser, AutoAssigner autoAssigner, TimeProvider clock)
    : IRequestHandler<ClaimTicketCommand, Result>,
      IRequestHandler<AssignTicketCommand, Result>,
      IRequestHandler<MoveTicketToTeamCommand, Result>
{
    private static readonly Role[] StaffRoles = [.. Policies.RolesByPolicy[Policies.CanWorkTickets]];

    public async Task<Result> Handle(ClaimTicketCommand request, CancellationToken cancellationToken)
    {
        var (ticket, error) = await LoadAsync(request.TicketId, cancellationToken);
        if (error is not null)
            return error;

        var agentId = currentUser.UserId!.Value;
        if (ticket!.AssigneeId is { } holder && holder != agentId)
            return TicketErrors.AlreadyAssigned;

        // A claim racing with another one fails on the row version (xmin) and becomes a 409 as well.
        ticket.Claim(agentId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(AssignTicketCommand request, CancellationToken cancellationToken)
    {
        var (ticket, error) = await LoadAsync(request.TicketId, cancellationToken);
        if (error is not null)
            return error;

        if (request.AssigneeId is { } assigneeId
            && !await db.Users.AnyAsync(u => u.Id == assigneeId && u.IsActive && u.Roles.Any(r => StaffRoles.Contains(r)), cancellationToken))
        {
            return TicketErrors.AssigneeNotStaff;
        }

        if (ticket!.IsConfidential && request.AssigneeId is { } confidentialAssignee
            && !await TicketAccess.IsInConfidentialGroupAsync(db, confidentialAssignee, cancellationToken))
        {
            return TicketErrors.AssigneeNotInConfidentialGroup;
        }

        ticket.Assign(request.AssigneeId, currentUser.UserId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> Handle(MoveTicketToTeamCommand request, CancellationToken cancellationToken)
    {
        var (ticket, error) = await LoadAsync(request.TicketId, cancellationToken);
        if (error is not null)
            return error;
        if (!await db.Teams.AnyAsync(t => t.Id == request.TeamId, cancellationToken))
            return TicketErrors.TeamNotFound;

        ticket!.MoveToTeam(request.TeamId, currentUser.UserId, clock.GetUtcNow());
        if (ticket.Status != Domain.Tickets.TicketStatus.PendingApproval)
            await autoAssigner.AssignAsync(ticket, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<(Domain.Tickets.Ticket? Ticket, Error? Error)> LoadAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(db, currentUser).SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
            return (null, TicketErrors.NotFound);
        if (!TicketAccess.IsStaff(currentUser))
            return (null, TicketErrors.StaffOnly);
        if (ticket.IsFinal)
            return (null, TicketErrors.NotEditable);
        return (ticket, null);
    }
}
