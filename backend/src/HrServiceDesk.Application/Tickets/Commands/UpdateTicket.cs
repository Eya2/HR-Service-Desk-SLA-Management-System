using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>
/// The requester can reword a case while it is still New; HR staff can edit it and change its priority
/// until it is closed. Status changes are a separate operation (status machine).
/// </summary>
public sealed record UpdateTicketCommand(Guid Id, string Title, string? Description, string? Priority) : IRequest<Result>;

internal sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(Ticket.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(Ticket.DescriptionMaxLength);
        RuleFor(c => c.Priority).Must(p => p is null || Enum.TryParse<TicketPriority>(p, ignoreCase: false, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<TicketPriority>())}.");
    }
}

internal sealed class UpdateTicketHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<UpdateTicketCommand, Result>
{
    public async Task<Result> Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(db, currentUser).SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;
        if (!TicketAccess.CanParticipate(ticket, currentUser))
            return TicketErrors.ReadOnly;
        if (ticket.IsFinal)
            return TicketErrors.NotEditable;

        var isStaff = TicketAccess.IsStaff(currentUser);
        var actorId = currentUser.UserId!.Value;
        var now = clock.GetUtcNow();
        if (!isStaff && ticket.Status != TicketStatus.New)
            return TicketErrors.NotEditable;

        if (request.Priority is not null)
        {
            var priority = Enum.Parse<TicketPriority>(request.Priority);
            if (priority != ticket.Priority)
            {
                if (!isStaff)
                    return TicketErrors.PriorityForbidden;
                ticket.ChangePriority(priority, actorId, now);
            }
        }

        ticket.UpdateDetails(request.Title, request.Description, actorId, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
