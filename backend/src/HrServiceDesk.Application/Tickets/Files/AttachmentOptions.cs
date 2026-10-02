namespace HrServiceDesk.Application.Tickets.Files;

/// <summary>Upload limits, bound from the <c>Attachments</c> configuration section.</summary>
public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxFilesPerRequest { get; set; } = 10;
}
