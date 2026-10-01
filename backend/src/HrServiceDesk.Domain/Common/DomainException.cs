namespace HrServiceDesk.Domain.Common;

/// <summary>Raised when a domain invariant is violated. <see cref="Code"/> is a stable, machine-readable key.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
