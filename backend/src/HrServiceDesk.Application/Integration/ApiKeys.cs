using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Audit;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Audit;
using HrServiceDesk.Domain.Integration;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Integration;

public sealed record ApiKeyDto(Guid Id, string Name, string Prefix, IReadOnlyList<string> Scopes, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);

/// <summary>The new key with its secret, which is shown this one time only.</summary>
public sealed record ApiKeyCreatedDto(ApiKeyDto Key, string Secret);

public sealed record ListApiKeysQuery : IRequest<IReadOnlyList<ApiKeyDto>>;

public sealed record CreateApiKeyCommand(string Name, IReadOnlyList<string> Scopes) : IRequest<Result<ApiKeyCreatedDto>>;

public sealed record RevokeApiKeyCommand(Guid Id) : IRequest<Result>;

internal sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(ApiKey.NameMaxLength);
        RuleFor(c => c.Scopes).NotEmpty();
        RuleForEach(c => c.Scopes).Must(s => ApiScopes.All.Contains(s)).WithMessage($"Scopes must be among: {string.Join(", ", ApiScopes.All)}.");
    }
}

internal sealed class ApiKeyHandlers(IAppDbContext db, ICurrentUser currentUser, ITenantContext tenantContext, IPasswordHasher hasher, AuditTrail audit, TimeProvider clock)
    : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>,
      IRequestHandler<CreateApiKeyCommand, Result<ApiKeyCreatedDto>>,
      IRequestHandler<RevokeApiKeyCommand, Result>
{
    private static readonly Error NotFound = Error.NotFound("api_key.not_found", "API key not found.");

    public async Task<IReadOnlyList<ApiKeyDto>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken) =>
        (await db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<Result<ApiKeyCreatedDto>> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var serviceUser = await ApiKeyServiceAccount.CreateAsync(db, hasher, tenantId, request.Name, cancellationToken);
        var (key, secret) = ApiKey.Issue(request.Name, request.Scopes, serviceUser.Id, currentUser.UserId);
        db.ApiKeys.Add(key);
        audit.Add(AuditAction.ApiKeyCreated, nameof(ApiKey), key.Id, $"API key \"{key.Name}\" ({key.Prefix}) created with {string.Join(", ", key.Scopes)}");
        await db.SaveChangesAsync(cancellationToken);
        return new ApiKeyCreatedDto(ToDto(key), secret);
    }

    public async Task<Result> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.SingleOrDefaultAsync(k => k.Id == request.Id, cancellationToken);
        if (key is null)
            return NotFound;
        if (!key.IsActive)
            return Result.Success();

        key.Revoke(clock.GetUtcNow());
        audit.Add(AuditAction.ApiKeyRevoked, nameof(ApiKey), key.Id, $"API key \"{key.Name}\" ({key.Prefix}) revoked");
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ApiKeyDto ToDto(ApiKey k) => new(k.Id, k.Name, k.Prefix, k.Scopes, k.CreatedAt, k.LastUsedAt, k.RevokedAt);
}

/// <summary>
/// Each key writes as its own inactive "service account" user (e.g. "Payroll connector (API)"), so comments and
/// history show which system acted. The account cannot sign in.
/// </summary>
public static class ApiKeyServiceAccount
{
    public static async Task<User> CreateAsync(IAppDbContext db, IPasswordHasher hasher, Guid tenantId, string keyName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);
        var slug = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Slug).SingleAsync(cancellationToken);
        var name = keyName.Trim();
        if (name.Length > 80)
            name = name[..80];
        var user = User.Create($"integration.{Guid.NewGuid():N}@{slug}.invalid", name, "(API)", [Role.Employee]);
        user.TenantId = tenantId;
        user.SetPasswordHash(hasher.Hash(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));
        user.Deactivate();
        db.Users.Add(user);
        return user;
    }
}
