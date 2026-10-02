namespace HrServiceDesk.Application.Abstractions;

/// <summary>Allocates the next case reference (e.g. <c>HR-2026-000123</c>) atomically, without gaps between concurrent callers.</summary>
public interface IReferenceNumberGenerator
{
    Task<string> NextTicketReferenceAsync(Guid tenantId, CancellationToken cancellationToken);
}
