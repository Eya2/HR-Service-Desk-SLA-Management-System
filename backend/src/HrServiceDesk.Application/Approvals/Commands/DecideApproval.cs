using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Approvals.Commands;

/// <summary>Approves or rejects the current approval step of a case. Rejecting needs a comment.</summary>
public sealed record DecideApprovalCommand(Guid TicketId, Guid ApprovalId, bool Approve, string? Comment) : IRequest<Result>;

internal sealed class DecideApprovalValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalValidator()
    {
        RuleFor(c => c.Comment).MaximumLength(TicketApproval.CommentMaxLength);
        RuleFor(c => c.Comment).NotEmpty().When(c => !c.Approve).WithMessage("A reason is required to reject a request.");
    }
}

internal sealed class DecideApprovalHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<DecideApprovalCommand, Result>
{
    private static readonly Error NotApprover = Error.Forbidden("approval.not_approver", "You are not an approver for this step.");
    private static readonly Error NotCurrent = Error.Conflict("approval.not_current", "This approval step is no longer waiting for a decision.");

    public async Task<Result> Handle(DecideApprovalCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(db, currentUser)
            .Include(t => t.Approvals)
            .SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;

        var userId = currentUser.UserId!.Value;
        var current = ticket.CurrentApproval;
        if (current is null || current.Id != request.ApprovalId)
            return NotCurrent;
        if (userId == ticket.RequesterId || !current.IsApprover(userId, currentUser.Roles))
            return NotApprover;

        ticket.DecideApproval(request.ApprovalId, request.Approve, userId, currentUser.Roles, request.Comment, clock.GetUtcNow());
        if (!request.Approve)
            ticket.AddComment(userId, request.Comment!, isInternal: false, clock.GetUtcNow());

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
