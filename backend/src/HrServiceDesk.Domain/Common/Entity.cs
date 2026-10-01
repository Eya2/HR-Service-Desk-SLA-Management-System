namespace HrServiceDesk.Domain.Common;

/// <summary>Base type for all persisted entities. Identity is a client-generated GUID.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
