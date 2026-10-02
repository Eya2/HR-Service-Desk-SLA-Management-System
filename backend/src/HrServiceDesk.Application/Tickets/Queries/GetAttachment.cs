using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Tickets.Queries;

/// <summary>Opens an attachment for download, if the caller can see its case.</summary>
public sealed record GetAttachmentQuery(Guid TicketId, Guid AttachmentId) : IRequest<Result<AttachmentContent>>;

internal sealed class GetAttachmentHandler(IAppDbContext db, ICurrentUser currentUser, IFileStorage storage, Audit.AuditTrail audit)
    : IRequestHandler<GetAttachmentQuery, Result<AttachmentContent>>
{
    public async Task<Result<AttachmentContent>> Handle(GetAttachmentQuery request, CancellationToken cancellationToken)
    {
        var found = await (
            from t in db.Tickets.AsNoTracking().VisibleTo(db, currentUser)
            join a in db.Attachments.AsNoTracking() on t.Id equals a.TicketId
            where t.Id == request.TicketId && a.Id == request.AttachmentId
            select new { Attachment = a, t.Reference, t.IsSensitive, t.RequesterId }).SingleOrDefaultAsync(cancellationToken);
        if (found is null)
            return TicketErrors.AttachmentNotFound;
        var attachment = found.Attachment;

        if (found.IsSensitive && found.RequesterId != currentUser.UserId)
        {
            await audit.WriteAsync(Domain.Audit.AuditAction.SensitiveAttachmentDownloaded, "Attachment", attachment.Id,
                $"Document of {found.Reference} downloaded", cancellationToken);
        }

        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return new AttachmentContent(attachment.FileName, attachment.ContentType, content);
    }
}
