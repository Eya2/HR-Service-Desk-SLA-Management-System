using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>An HR case. Comments and attachments are only added through the aggregate.</summary>
public sealed class Ticket : Entity, ITenantOwned, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private readonly List<Comment> _comments = [];
    private readonly List<Attachment> _attachments = [];

    private Ticket() { }

    public Guid TenantId { get; set; }

    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid RequestTypeId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; } = TicketStatus.New;

    public Guid RequesterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    /// <summary>Copied from the request type at submission, so later catalog edits do not expose existing cases.</summary>
    public bool IsConfidential { get; private set; }

    /// <summary>The validated answers to the request type's form (jsonb). File fields hold attachment ids.</summary>
    public string FormData { get; private set; } = "{}";

    /// <summary>Closed, cancelled or rejected cases accept no further changes.</summary>
    public bool IsFinal => Status is TicketStatus.Closed or TicketStatus.Cancelled or TicketStatus.Rejected;

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static Ticket Submit(string reference, RequestType requestType, Guid requesterId, string title, string? description, string formData)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        if (!requestType.IsActive)
            throw new DomainException("ticket.request_type_inactive", "This request type is no longer available.");

        var ticket = new Ticket
        {
            Reference = reference,
            RequestTypeId = requestType.Id,
            RequesterId = requesterId,
            Priority = requestType.DefaultPriority,
            IsConfidential = requestType.IsConfidential,
            FormData = formData,
        };
        ticket.UpdateDetails(title, description);
        return ticket;
    }

    public void UpdateDetails(string title, string? description)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        if (title.Length is 0 or > TitleMaxLength)
            throw new DomainException("ticket.invalid_title", $"Title must be 1 to {TitleMaxLength} characters.");
        if (description.Length > DescriptionMaxLength)
            throw new DomainException("ticket.invalid_description", $"Description must be at most {DescriptionMaxLength} characters.");
        Title = title;
        Description = description;
    }

    public void ChangePriority(TicketPriority priority) => Priority = priority;

    public void SetFormData(string formData) => FormData = formData;

    public Comment AddComment(Guid authorId, string body, bool isInternal, DateTimeOffset now)
    {
        body = (body ?? string.Empty).Trim();
        if (body.Length is 0 or > Comment.BodyMaxLength)
            throw new DomainException("comment.invalid_body", $"A comment must be 1 to {Comment.BodyMaxLength} characters.");

        var comment = new Comment(Id, authorId, body, isInternal, now);
        _comments.Add(comment);
        return comment;
    }

    public Attachment AddAttachment(
        string fileName, string contentType, long sizeBytes, string storageKey, Guid uploadedById, string? fieldKey, DateTimeOffset now)
    {
        var attachment = new Attachment(Id, fileName, contentType, sizeBytes, storageKey, uploadedById, fieldKey, now);
        _attachments.Add(attachment);
        return attachment;
    }
}
