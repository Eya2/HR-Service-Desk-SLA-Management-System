using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets.Files;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>Adds supporting documents to an existing case.</summary>
public sealed record AddAttachmentsCommand(Guid TicketId, IReadOnlyList<UploadedFile> Files) : IRequest<Result<IReadOnlyList<AttachmentDto>>>;

internal sealed class AddAttachmentsHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IFileStorage storage,
    TimeProvider clock,
    IOptions<AttachmentOptions> options) : IRequestHandler<AddAttachmentsCommand, Result<IReadOnlyList<AttachmentDto>>>
{
    public async Task<Result<IReadOnlyList<AttachmentDto>>> Handle(AddAttachmentsCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.VisibleTo(currentUser).SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken);
        if (ticket is null)
            return TicketErrors.NotFound;
        if (!TicketAccess.CanParticipate(ticket, currentUser))
            return TicketErrors.ReadOnly;
        if (ticket.IsFinal)
            return TicketErrors.NotEditable;

        // Files added after submission are case documents, not answers to a form field.
        var files = request.Files.Select(f => f with { FieldName = string.Empty }).ToList();
        if (files.Count == 0)
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("files", "At least one file is required.")]);
        var checkedFiles = TicketFiles.CheckOrThrow(files, options.Value);

        var uploaderId = currentUser.UserId!.Value;
        var attachments = await TicketFiles.StoreAsync(
            storage, tenantContext.TenantId!.Value, ticket, checkedFiles, uploaderId, clock.GetUtcNow(), cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TicketFiles.DeleteQuietlyAsync(storage, attachments.Select(a => a.StorageKey));
            throw;
        }

        var uploader = await db.Users.AsNoTracking().Where(u => u.Id == uploaderId)
            .Select(u => u.FirstName + " " + u.LastName).SingleAsync(cancellationToken);
        return attachments
            .Select(a => new AttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.FieldKey, uploader, a.CreatedAt))
            .ToList();
    }
}
