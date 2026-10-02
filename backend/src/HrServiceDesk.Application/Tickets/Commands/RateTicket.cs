using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>The requester rates a closed case once (1 to 5, optional comment).</summary>
public sealed record RateTicketCommand(Guid TicketId, int Score, string? Comment) : IRequest<Result>;

internal sealed class RateTicketValidator : AbstractValidator<RateTicketCommand>
{
    public RateTicketValidator()
    {
        RuleFor(c => c.Score).InclusiveBetween(1, 5);
        RuleFor(c => c.Comment).MaximumLength(SatisfactionRating.CommentMaxLength);
    }
}

internal sealed class RateTicketHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IRequestHandler<RateTicketCommand, Result>
{
    private static readonly Error AlreadyRated = Error.Conflict("rating.already_given", "This case has already been rated.");

    public async Task<Result> Handle(RateTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(db, currentUser).SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;
        if (await db.SatisfactionRatings.AnyAsync(r => r.TicketId == ticket.Id, cancellationToken))
            return AlreadyRated;

        db.SatisfactionRatings.Add(SatisfactionRating.Create(ticket, currentUser.UserId!.Value, request.Score, request.Comment, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
