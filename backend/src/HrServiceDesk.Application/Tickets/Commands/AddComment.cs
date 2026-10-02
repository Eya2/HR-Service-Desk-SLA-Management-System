using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>Adds a public reply or, for HR staff, an internal note.</summary>
public sealed record AddCommentCommand(Guid TicketId, string Body, bool IsInternal) : IRequest<Result<CommentDto>>;

internal sealed class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(Comment.BodyMaxLength);
}

internal sealed class AddCommentHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
    public async Task<Result<CommentDto>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(currentUser).SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;
        if (!TicketAccess.CanParticipate(ticket, currentUser))
            return TicketErrors.ReadOnly;
        if (request.IsInternal && !TicketAccess.IsStaff(currentUser))
            return TicketErrors.InternalCommentForbidden;
        if (ticket.IsFinal)
            return TicketErrors.NotEditable;

        var authorId = currentUser.UserId!.Value;
        var comment = ticket.AddComment(authorId, request.Body, request.IsInternal, clock.GetUtcNow());
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        var author = await db.Users.AsNoTracking().Where(u => u.Id == authorId)
            .Select(u => u.FirstName + " " + u.LastName).SingleAsync(cancellationToken);
        return new CommentDto(comment.Id, authorId, author, comment.Body, comment.IsInternal, comment.CreatedAt);
    }
}
