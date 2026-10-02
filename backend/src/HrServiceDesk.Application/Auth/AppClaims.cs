namespace HrServiceDesk.Application.Auth;

/// <summary>Claim types carried by access tokens. Inbound claim mapping is disabled, so these are the raw JWT names.</summary>
public static class AppClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Tenant = "tenant_id";
    public const string Role = "role";
}
