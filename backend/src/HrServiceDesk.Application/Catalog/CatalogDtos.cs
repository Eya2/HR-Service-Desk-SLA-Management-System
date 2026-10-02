using HrServiceDesk.Domain.Catalog;

namespace HrServiceDesk.Application.Catalog;

public sealed record RequestTypeSummaryDto(
    Guid Id, string Name, string Description, string Category, bool IsConfidential, bool IsActive, string DefaultPriority);

public sealed record RequestTypeDto(
    Guid Id,
    string Name,
    string Description,
    string Category,
    bool IsConfidential,
    bool IsActive,
    string DefaultPriority,
    IReadOnlyList<FormField> Fields);
