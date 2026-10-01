namespace HrServiceDesk.Domain.Common;

/// <summary>Creation and modification timestamps, stamped by the persistence layer.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}
