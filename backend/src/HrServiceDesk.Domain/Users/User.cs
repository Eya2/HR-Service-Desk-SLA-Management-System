using System.Net.Mail;
using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Users;

/// <summary>A person who signs in. Users belong to one tenant; e-mail addresses are unique platform-wide.</summary>
public sealed class User : Entity, ITenantOwned, IAuditable
{
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 100;

    private User() { }

    public Guid TenantId { get; set; }

    /// <summary>Lower-cased, trimmed e-mail address used as the login.</summary>
    public string Email { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";

    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Distinct, sorted roles. Replaced as a whole through <see cref="SetRoles"/> (an array maps to a PostgreSQL array).</summary>
    public Role[] Roles { get; private set; } = [];

    /// <summary>Direct manager, who approves "Manager" workflow steps for this user's requests.</summary>
    public Guid? ManagerId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutEndsAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static User Create(string email, string firstName, string lastName, IEnumerable<Role> roles)
    {
        var user = new User { Email = NormalizeEmail(email) };
        user.Rename(firstName, lastName);
        user.SetRoles(roles);
        return user;
    }

    public static string NormalizeEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length is 0 or > EmailMaxLength || !MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized)
            throw new DomainException("user.invalid_email", "E-mail address is not valid.");
        return normalized;
    }

    public bool HasRole(Role role) => Roles.Contains(role);

    public void Rename(string firstName, string lastName)
    {
        FirstName = RequireName(firstName, "first name");
        LastName = RequireName(lastName, "last name");
    }

    public void SetRoles(IEnumerable<Role> roles)
    {
        var distinct = (roles ?? []).Distinct().Order().ToArray();
        if (distinct.Length == 0)
            throw new DomainException("user.no_role", "A user needs at least one role.");
        Roles = distinct;
    }

    public void SetManager(Guid? managerId)
    {
        if (managerId == Id)
            throw new DomainException("user.self_manager", "A user cannot be their own manager.");
        ManagerId = managerId;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("user.invalid_password_hash", "Password hash is required.");
        PasswordHash = passwordHash;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } end && end > now;

    /// <summary>Counts a failed sign-in and locks the account once <paramref name="maxAttempts"/> is reached.</summary>
    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEndsAt = now + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        LastLoginAt = now;
    }

    private static string RequireName(string value, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
            throw new DomainException("user.invalid_name", $"The {label} must be 1 to {NameMaxLength} characters.");
        return trimmed;
    }
}
