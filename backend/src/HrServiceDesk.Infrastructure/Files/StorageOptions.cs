namespace HrServiceDesk.Infrastructure.Files;

/// <summary>Bound from the <c>Storage</c> configuration section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Directory for attachment contents (a Docker volume in the compose setup).</summary>
    public string RootPath { get; set; } = "data/attachments";
}
