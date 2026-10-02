using FluentValidation;
using FluentValidation.Results;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Application.Tickets.Files;

internal static class TicketFiles
{
    /// <summary>Throws a validation error (400) listing every rejected file.</summary>
    public static List<CheckedFile> CheckOrThrow(IReadOnlyList<UploadedFile> files, AttachmentOptions options)
    {
        var (accepted, errors) = FilePolicy.Check(files, options);
        if (errors.Count > 0)
            throw new ValidationException(errors.Select(e => new ValidationFailure(PropertyFor(e.Field), e.Message)));
        return accepted;
    }

    public static string PropertyFor(string field) => field == "files" ? "files" : $"values.{field}";

    /// <summary>
    /// Writes the files to storage and records them on the ticket. If anything fails, files already
    /// written are deleted so storage and database stay consistent.
    /// </summary>
    public static async Task<List<Attachment>> StoreAsync(
        IFileStorage storage, Guid tenantId, Ticket ticket, IEnumerable<CheckedFile> files, Guid uploaderId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var stored = new List<string>();
        var attachments = new List<Attachment>();
        try
        {
            foreach (var file in files)
            {
                await using var content = file.File.OpenReadStream();
                var key = await storage.SaveAsync(tenantId, content, cancellationToken);
                stored.Add(key);
                var fieldKey = string.IsNullOrEmpty(file.File.FieldName) ? null : file.File.FieldName;
                attachments.Add(ticket.AddAttachment(file.SafeFileName, file.ContentType, file.File.Length, key, uploaderId, fieldKey, now));
            }

            return attachments;
        }
        catch
        {
            await DeleteQuietlyAsync(storage, stored);
            throw;
        }
    }

    public static async Task DeleteQuietlyAsync(IFileStorage storage, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
#pragma warning disable CA1031 // Best-effort cleanup must not hide the original failure.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }
    }
}
