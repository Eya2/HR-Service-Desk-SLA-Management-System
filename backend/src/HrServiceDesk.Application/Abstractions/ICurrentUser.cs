using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Application.Abstractions;

/// <summary>The authenticated caller, read from the access token.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(Role role);
}
