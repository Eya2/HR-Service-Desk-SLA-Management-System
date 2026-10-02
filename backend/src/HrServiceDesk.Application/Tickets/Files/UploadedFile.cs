namespace HrServiceDesk.Application.Tickets.Files;

/// <summary>A file received from the client. <see cref="FieldName"/> is the form field it answers (empty for plain attachments).</summary>
public sealed record UploadedFile(string FieldName, string FileName, long Length, Func<Stream> OpenReadStream);
