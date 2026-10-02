using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private static User NewUser(params Role[] roles) =>
        User.Create("Amira.BenSalah@Acme.Example ", "Amira", "Ben Salah", roles.Length == 0 ? [Role.Employee] : roles);

    [Fact]
    public void Create_normalizes_email_and_names()
    {
        var user = User.Create("  Amira.BenSalah@ACME.example ", " Amira ", " Ben Salah ", [Role.Employee]);

        user.Email.Should().Be("amira.bensalah@acme.example");
        user.FullName.Should().Be("Amira Ben Salah");
        user.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b@c.example")]
    [InlineData("Name <a@b.example>")]
    public void Create_rejects_invalid_email(string email)
    {
        var act = () => User.Create(email, "A", "B", [Role.Employee]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("user.invalid_email");
    }

    [Fact]
    public void Roles_are_distinct_and_sorted()
    {
        var user = NewUser(Role.HrAdmin, Role.Employee, Role.HrAdmin);

        user.Roles.Should().Equal(Role.Employee, Role.HrAdmin);
        user.HasRole(Role.HrAdmin).Should().BeTrue();
        user.HasRole(Role.Manager).Should().BeFalse();
    }

    [Fact]
    public void A_user_needs_at_least_one_role()
    {
        var user = NewUser();

        var act = () => user.SetRoles([]);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("user.no_role");
    }

    [Fact]
    public void A_user_cannot_be_their_own_manager()
    {
        var user = NewUser();

        var act = () => user.SetManager(user.Id);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("user.self_manager");
    }

    [Fact]
    public void Account_locks_when_max_failed_attempts_is_reached()
    {
        var user = NewUser();

        for (var i = 0; i < 4; i++)
            user.RegisterFailedLogin(Now, maxAttempts: 5, TimeSpan.FromMinutes(15));
        user.IsLockedOut(Now).Should().BeFalse();

        user.RegisterFailedLogin(Now, maxAttempts: 5, TimeSpan.FromMinutes(15));

        user.IsLockedOut(Now).Should().BeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).Should().BeFalse("the lockout ends after its duration");
    }

    [Fact]
    public void Successful_login_resets_failures_and_lockout()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now, maxAttempts: 1, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulLogin(Now);

        user.IsLockedOut(Now).Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        user.LastLoginAt.Should().Be(Now);
    }
}
