using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Users;

[Collection(PostgresCollection.Name)]
public sealed class UserAdministrationTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private sealed record UserSummary(Guid Id, string Email, string FullName, string[] Roles, bool IsActive);

    private sealed record UsersPage(UserSummary[] Items, int Page, int PageSize, int TotalCount);

    private sealed record UserDetails(Guid Id, string Email, string FirstName, string LastName, string[] Roles, Guid? ManagerId, string? ManagerName, bool IsActive);

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    [Theory]
    [InlineData(DemoUsers.AcmeEmployee)]
    [InlineData(DemoUsers.AcmeManager)]
    [InlineData(DemoUsers.AcmeHrOfficer)]
    [InlineData(DemoUsers.AcmeAuditor)]
    public async Task Only_hr_admins_can_manage_users(string email)
    {
        var client = await SignedInAs(email);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_admin_sees_only_users_of_their_own_organisation()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);

        var page = await client.GetFromJsonAsync<UsersPage>("/api/users?pageSize=100");

        page!.Items.Should().NotBeEmpty();
        page.Items.Should().OnlyContain(u => u.Email.EndsWith("@acme.example", StringComparison.Ordinal));
        page.Items.Should().Contain(u => u.Email == DemoUsers.AcmeManager);
    }

    [Fact]
    public async Task Users_can_be_searched_and_filtered_by_role()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);

        var byName = await client.GetFromJsonAsync<UsersPage>("/api/users?search=HADDAD");
        byName!.Items.Should().ContainSingle().Which.Email.Should().Be(DemoUsers.AcmeManager);

        var managers = await client.GetFromJsonAsync<UsersPage>("/api/users?role=Manager");
        managers!.Items.Should().ContainSingle().Which.Roles.Should().Contain("Manager");

        (await client.GetAsync("/api/users?role=Wizard")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_of_another_organisation_is_not_found()
    {
        var globex = await SignedInAs(DemoUsers.GlobexHrAdmin);
        var globexUsers = await globex.GetFromJsonAsync<UsersPage>("/api/users?pageSize=100");
        var globexUserId = globexUsers!.Items[0].Id;

        var acme = await SignedInAs(DemoUsers.AcmeHrAdmin);

        (await acme.GetAsync($"/api/users/{globexUserId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var update = await acme.PutAsJsonAsync($"/api/users/{globexUserId}",
            new { firstName = "X", lastName = "Y", roles = new[] { "Employee" }, managerId = (Guid?)null, isActive = false });
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_user_with_manager_returns_details()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var manager = (await client.GetFromJsonAsync<UsersPage>("/api/users?role=Manager"))!.Items.Single();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = $"Created.{Guid.NewGuid():N}@Acme.example",
            firstName = "Created",
            lastName = "User",
            roles = new[] { "Employee", "HrOfficer" },
            managerId = manager.Id,
            password = TestUsers.Password,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = (await response.Content.ReadFromJsonAsync<UserDetails>())!;
        created.Email.Should().StartWith("created.").And.EndWith("@acme.example");
        created.Roles.Should().Equal("Employee", "HrOfficer");
        created.ManagerName.Should().Be("Youssef Haddad");
    }

    [Fact]
    public async Task Create_user_rejects_weak_password_duplicate_email_and_foreign_manager()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var globex = await SignedInAs(DemoUsers.GlobexHrAdmin);
        var foreignManager = (await globex.GetFromJsonAsync<UsersPage>("/api/users?role=Manager"))!.Items.Single();

        object Body(string email, string password, Guid? managerId = null) =>
            new { email, firstName = "A", lastName = "B", roles = new[] { "Employee" }, managerId, password };

        var weak = await client.PostAsJsonAsync("/api/users", Body($"weak.{Guid.NewGuid():N}@acme.example", "password"));
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.Content.ReadAsStringAsync()).Should().Contain("Password");

        // E-mails are unique platform-wide, so a Globex address cannot be reused at Acme.
        var duplicate = await client.PostAsJsonAsync("/api/users", Body(DemoUsers.GlobexEmployee, TestUsers.Password));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).Should().Be("user.email_taken");

        var foreign = await client.PostAsJsonAsync("/api/users", Body($"f.{Guid.NewGuid():N}@acme.example", TestUsers.Password, foreignManager.Id));
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await foreign.ProblemCodeAsync()).Should().Be("user.manager_not_found");
    }

    [Fact]
    public async Task Hr_admin_cannot_grant_super_admin()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            email = $"sa.{Guid.NewGuid():N}@acme.example",
            firstName = "A",
            lastName = "B",
            roles = new[] { "SuperAdmin" },
            managerId = (Guid?)null,
            password = TestUsers.Password,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hr_admin_cannot_deactivate_themselves()
    {
        var client = _api.CreateApiClient();
        var signedIn = await client.LoginAsync(DemoUsers.AcmeHrAdmin);
        client.Authorize(signedIn);

        var response = await client.PutAsJsonAsync($"/api/users/{signedIn.Session.User.Id}",
            new { firstName = "Nadia", lastName = "Jaziri", roles = new[] { "Employee", "HrAdmin" }, managerId = (Guid?)null, isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("user.cannot_deactivate_self");
    }

    [Fact]
    public async Task Deactivating_a_user_ends_their_sessions_and_blocks_login()
    {
        var email = await TestUsers.CreateAsync(_api, "deactivate");
        var userClient = _api.CreateApiClient();
        var userSession = await userClient.LoginAsync(email, TestUsers.Password);
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);

        var response = await admin.PutAsJsonAsync($"/api/users/{userSession.Session.User.Id}",
            new { firstName = "Test", lastName = "deactivate", roles = new[] { "Employee" }, managerId = (Guid?)null, isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await userClient.PostRefreshAsync(userSession.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await userClient.PostLoginAsync(email, TestUsers.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_password_unlocks_and_replaces_the_password()
    {
        var email = await TestUsers.CreateAsync(_api, "reset");
        var userClient = _api.CreateApiClient();
        var userId = (await userClient.LoginAsync(email, TestUsers.Password)).Session.User.Id;
        for (var i = 0; i < 5; i++)
            await userClient.PostLoginAsync(email, "Wrong-Passw0rd!");
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);

        var response = await admin.PostAsJsonAsync($"/api/users/{userId}/reset-password", new { newPassword = "Brand-New-Passw0rd!" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await userClient.PostLoginAsync(email, "Brand-New-Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
