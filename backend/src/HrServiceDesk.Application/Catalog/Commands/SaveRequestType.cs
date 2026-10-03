using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Catalog.Commands;

/// <summary>Creates a request type (<see cref="Id"/> null) or updates an existing one. HR Admin only.</summary>
public sealed record SaveRequestTypeCommand(
    Guid? Id,
    string Name,
    string? Description,
    string Category,
    bool IsConfidential,
    string DefaultPriority,
    IReadOnlyList<FormField> Fields,
    bool IsSensitive = false) : IRequest<Result<RequestTypeDto>>;

internal sealed class SaveRequestTypeValidator : AbstractValidator<SaveRequestTypeCommand>
{
    public SaveRequestTypeValidator()
    {
        CatalogRules.ValidName(RuleFor(c => c.Name));
        CatalogRules.ValidDescription(RuleFor(c => c.Description));
        CatalogRules.ValidCategory(RuleFor(c => c.Category));
        CatalogRules.ValidPriority(RuleFor(c => c.DefaultPriority));
        RuleFor(c => c.Fields).NotNull();
    }
}

internal sealed class SaveRequestTypeHandler(IAppDbContext db) : IRequestHandler<SaveRequestTypeCommand, Result<RequestTypeDto>>
{
    public async Task<Result<RequestTypeDto>> Handle(SaveRequestTypeCommand request, CancellationToken cancellationToken)
    {
        // Throws a DomainException (422, code form_schema.invalid) describing every problem in the definition.
        var schema = FormSchema.Create(request.Fields);
        var category = Enum.Parse<RequestCategory>(request.Category);
        var priority = Enum.Parse<TicketPriority>(request.DefaultPriority);

        RequestType type;
        if (request.Id is { } id)
        {
            var existing = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (existing is null)
                return CatalogRules.NotFound;
            type = existing;
            type.Update(request.Name, request.Description ?? string.Empty, category, request.IsConfidential, priority, schema);
        }
        else
        {
            type = RequestType.Create(request.Name, request.Description ?? string.Empty, category, request.IsConfidential, priority, schema);
            db.RequestTypes.Add(type);
        }

        type.MarkSensitive(request.IsSensitive);
        await db.SaveChangesAsync(cancellationToken);
        return CatalogRules.ToDto(type);
    }
}
