using System.Text;

namespace HrServiceDesk.Application.Tickets.Files;

public sealed record CheckedFile(UploadedFile File, string SafeFileName, string ContentType);

/// <summary>
/// Accepts only an allow-list of document and image types. The type is decided from the file's first
/// bytes (magic number) and must agree with its extension; the client's content type is ignored.
/// </summary>
public static class FilePolicy
{
    private sealed record Kind(string ContentType, byte[] Magic);

    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04];

    private static readonly Dictionary<string, Kind> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new("application/pdf", Pdf),
        [".png"] = new("image/png", Png),
        [".jpg"] = new("image/jpeg", Jpeg),
        [".jpeg"] = new("image/jpeg", Jpeg),
        [".docx"] = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Zip),
        [".xlsx"] = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Zip),
    };

    public static IReadOnlyCollection<string> AllowedExtensions => Allowed.Keys;

    /// <summary>Returns the accepted files, or one error message per rejected file.</summary>
    public static (List<CheckedFile> Accepted, List<(string Field, string Message)> Errors) Check(
        IReadOnlyList<UploadedFile> files, AttachmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(options);
        var accepted = new List<CheckedFile>();
        var errors = new List<(string, string)>();

        if (files.Count > options.MaxFilesPerRequest)
        {
            errors.Add(("files", $"At most {options.MaxFilesPerRequest} files can be sent at once."));
            return (accepted, errors);
        }

        foreach (var file in files)
        {
            var field = string.IsNullOrEmpty(file.FieldName) ? "files" : file.FieldName;
            var name = SafeFileName(file.FileName);

            if (file.Length == 0)
                errors.Add((field, $"'{name}' is empty."));
            else if (file.Length > options.MaxFileSizeBytes)
                errors.Add((field, $"'{name}' exceeds the {options.MaxFileSizeBytes / (1024 * 1024)} MB limit."));
            else if (!Allowed.TryGetValue(Path.GetExtension(name), out var kind))
                errors.Add((field, $"'{name}' is not an accepted file type ({string.Join(", ", Allowed.Keys)})."));
            else if (!StartsWith(file, kind.Magic))
                errors.Add((field, $"'{name}' content does not match its extension."));
            else
                accepted.Add(new CheckedFile(file, name, kind.ContentType));
        }

        return (accepted, errors);
    }

    /// <summary>Keeps only the base name, without control or path characters, at most 255 characters.</summary>
    public static string SafeFileName(string? fileName)
    {
        var baseName = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var builder = new StringBuilder(baseName.Length);
        foreach (var c in baseName)
        {
            if (!char.IsControl(c) && c is not ('/' or '"' or '<' or '>' or '|' or ':' or '*' or '?'))
                builder.Append(c);
        }

        var safe = builder.ToString().Trim().TrimStart('.');
        if (safe.Length == 0)
            return "file";
        if (safe.Length <= 255)
            return safe;
        var extension = Path.GetExtension(safe);
        return safe[..(255 - extension.Length)] + extension;
    }

    private static bool StartsWith(UploadedFile file, byte[] magic)
    {
        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[magic.Length];
        var read = 0;
        while (read < header.Length)
        {
            var n = stream.Read(header[read..]);
            if (n == 0)
                return false;
            read += n;
        }

        return header.SequenceEqual(magic);
    }
}
