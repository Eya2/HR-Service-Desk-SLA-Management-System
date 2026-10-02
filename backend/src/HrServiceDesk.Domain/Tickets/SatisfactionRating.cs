using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Tickets;

/// <summary>The employee's satisfaction with a closed case (1 to 5, optional comment). One per case.</summary>
public sealed class SatisfactionRating : Entity, ITenantOwned
{
    public const int CommentMaxLength = 1000;

    private SatisfactionRating() { }

    public Guid TenantId { get; set; }

    public Guid TicketId { get; private set; }

    public int Score { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Only the requester rates, only once the case is closed.</summary>
    public static SatisfactionRating Create(Ticket ticket, Guid userId, int score, string? comment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (ticket.RequesterId != userId)
            throw new DomainException("rating.not_requester", "Only the employee who asked can rate the case.");
        if (ticket.Status != TicketStatus.Closed)
            throw new DomainException("rating.not_closed", "A case can be rated once it is closed.");
        if (score is < 1 or > 5)
            throw new DomainException("rating.invalid_score", "The score must be between 1 and 5.");
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment?.Length > CommentMaxLength)
            throw new DomainException("rating.comment_too_long", $"The comment must be at most {CommentMaxLength} characters.");

        ticket.Record(TicketEventType.Rated, userId, now, new { score });
        return new SatisfactionRating
        {
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            Score = score,
            Comment = comment,
            CreatedAt = now,
        };
    }
}
