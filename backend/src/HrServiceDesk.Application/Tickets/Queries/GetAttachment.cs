using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

/// <summary>Opens an attachment for download, if the caller can see its case.</summary>
public sealed record GetAttachmentQuery(Guid TicketId, Guid AttachmentId) : IRequest<Result<AttachmentContent>>;

internal sealed class GetAttachmentHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage)
    : IRequestHandler<GetAttachmentQuery, Result<AttachmentContent>>
{
    public async Task<Result<AttachmentContent>> Handle(GetAttachmentQuery request, CancellationToken cancellationToken)
    {
        var attachment = await (
            from t in db.Tickets.AsNoTracking().VisibleTo(db, currentUser)
            join a in db.Attachments.AsNoTracking() on t.Id equals a.TicketId
            where t.Id == request.TicketId && a.Id == request.AttachmentId
            select a).SingleOrDefaultAsync(cancellationToken);
        if (attachment is null)
            return TicketErrors.AttachmentNotFound;

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentContent(attachment.FileName, attachment.ContentType, content);
    }
}
