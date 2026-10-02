using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>Metadata of a file stored outside the database. <see cref="StorageKey"/> is opaque and server-generated.</summary>
public sealed class Attachment : Entity, ITenantOwned
{
    public const int FileNameMaxLength = 255;

    private Attachment() { }

    internal Attachment(
        Guid ticketId, string fileName, string contentType, long sizeBytes, string storageKey,
        Guid uploadedById, string? fieldKey, DateTimeOffset createdAt)
    {
        TicketId = ticketId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedById = uploadedById;
        FieldKey = fieldKey;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public Guid UploadedById { get; private set; }

    /// <summary>The form field the file answers, or null for a file added later to the case.</summary>
    public string? FieldKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
