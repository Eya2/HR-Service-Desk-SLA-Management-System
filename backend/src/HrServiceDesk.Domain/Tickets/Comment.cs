using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>A reply on a case. Internal comments are notes between HR staff, never shown to the employee.</summary>
public sealed class Comment : Entity, ITenantOwned
{
    public const int BodyMaxLength = 4000;

    private Comment() { }

    internal Comment(Guid ticketId, Guid authorId, string body, bool isInternal, DateTimeOffset createdAt)
    {
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body;
        IsInternal = isInternal;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public bool IsInternal { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
