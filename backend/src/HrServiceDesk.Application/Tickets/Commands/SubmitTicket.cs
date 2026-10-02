using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Results;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Tickets.Files;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Application.Tickets.Commands;

/// <summary>
/// An employee submits a request from the catalog: the form answers and the files for file fields
/// arrive together, so a case is never created without its mandatory documents.
/// </summary>
public sealed record SubmitTicketCommand(
    Guid RequestTypeId,
    string Title,
    string? Description,
    JsonObject? Values,
    IReadOnlyList<UploadedFile> Files) : IRequest<Result<TicketCreatedDto>>;

internal sealed class SubmitTicketValidator : AbstractValidator<SubmitTicketCommand>
{
    public SubmitTicketValidator()
    {
        RuleFor(c => c.RequestTypeId).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(Ticket.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(Ticket.DescriptionMaxLength);
    }
}

internal sealed class SubmitTicketHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IReferenceNumberGenerator references,
    IFileStorage storage,
    TimeProvider clock,
    IOptions<AttachmentOptions> attachmentOptions) : IRequestHandler<SubmitTicketCommand, Result<TicketCreatedDto>>
{
    public async Task<Result<TicketCreatedDto>> Handle(SubmitTicketCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } requesterId || tenantContext.TenantId is not { } tenantId)
            return AuthErrors.NotAuthenticated;

        var type = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == request.RequestTypeId && t.IsActive, cancellationToken);
        if (type is null)
            return TicketErrors.RequestTypeNotFound;

        var files = TicketFiles.CheckOrThrow(request.Files, attachmentOptions.Value);
        var fileCounts = files.GroupBy(f => f.File.FieldName).ToDictionary(g => g.Key, g => g.Count());
        var submission = type.Schema.Validate(request.Values, fileCounts);
        if (!submission.IsValid)
            throw new ValidationException(submission.Errors.Select(e => new ValidationFailure(TicketFiles.PropertyFor(e.Field), e.Message)));

        // The reference counter row stays locked until commit: numbers are sequential and gap-free.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var reference = await references.NextTicketReferenceAsync(tenantId, cancellationToken);
        var ticket = Ticket.Submit(reference, type, requesterId, request.Title, request.Description, "{}");
        var attachments = await TicketFiles.StoreAsync(storage, tenantId, ticket, files, requesterId, clock.GetUtcNow(), cancellationToken);

        // File fields are answered by the ids of their attachments.
        var values = submission.Values;
        foreach (var group in attachments.GroupBy(a => a.FieldKey!))
            values[group.Key] = new JsonArray(group.Select(a => (JsonNode)JsonValue.Create(a.Id.ToString())!).ToArray());
        ticket.SetFormData(values.ToJsonString());

        db.Tickets.Add(ticket);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await TicketFiles.DeleteQuietlyAsync(storage, attachments.Select(a => a.StorageKey));
            throw;
        }

        return new TicketCreatedDto(ticket.Id, ticket.Reference);
    }
}
