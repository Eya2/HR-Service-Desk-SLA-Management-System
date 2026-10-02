using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>
/// Moves a case through the status machine. A reason is required to reject a case; when given, it is
/// also posted as a public reply so the employee sees it.
/// </summary>
public sealed record ChangeTicketStatusCommand(Guid TicketId, string Status, string? Reason) : IRequest<Result>;

internal sealed class ChangeTicketStatusValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusValidator()
    {
        RuleFor(c => c.Status).Must(s => Enum.TryParse<TicketStatus>(s, ignoreCase: false, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<TicketStatus>())}.");
        RuleFor(c => c.Reason).MaximumLength(Comment.BodyMaxLength);
        RuleFor(c => c.Reason).NotEmpty().When(c => c.Status == nameof(TicketStatus.Rejected))
            .WithMessage("A reason is required to reject a case.");
    }
}

internal sealed class ChangeTicketStatusHandler(IAppDbContext db, ICurrentUser currentUser, Sla.SlaService sla, TimeProvider clock)
    : IRequestHandler<ChangeTicketStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeTicketStatusCommand request, CancellationToken cancellationToken)
    {
        // Approvals are loaded so that withdrawing or rejecting a pending case skips its remaining steps.
        var ticket = await db.Tickets.VisibleTo(db, currentUser)
            .Include(t => t.Approvals)
            .SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;

        var to = Enum.Parse<TicketStatus>(request.Status);
        var actor = TicketAccess.ActorFor(ticket, currentUser);
        switch (TicketStatusMachine.Check(ticket.Status, to, actor))
        {
            case TransitionCheck.Invalid:
                return TicketErrors.InvalidTransition(ticket.Status, to);
            case TransitionCheck.NotPermitted:
                return TicketErrors.TransitionNotPermitted(ticket.Status, to);
        }

        var actorId = currentUser.UserId!.Value;
        var now = clock.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(request.Reason))
            ticket.AddComment(actorId, request.Reason, isInternal: false, now);
        if (!string.IsNullOrWhiteSpace(request.Reason) && TicketAccess.IsStaff(currentUser) && actorId != ticket.RequesterId)
            ticket.RecordFirstResponse(now);
        ticket.ChangeStatus(to, actor, actorId, now, request.Reason);
        await sla.RefreshAsync(ticket, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
