using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Catalog.Commands;

/// <summary>Removes a type from (or puts it back into) the catalog. Existing cases are unaffected.</summary>
public sealed record SetRequestTypeActiveCommand(Guid Id, bool IsActive) : IRequest<Result>;

internal sealed class SetRequestTypeActiveHandler(IAppDbContext db) : IRequestHandler<SetRequestTypeActiveCommand, Result>
{
    public async Task<Result> Handle(SetRequestTypeActiveCommand request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (type is null)
            return CatalogRules.NotFound;

        if (request.IsActive)
            type.Activate();
        else
            type.Deactivate();

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
