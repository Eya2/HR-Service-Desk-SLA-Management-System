using System.Net;
using System.Net.Http.Json;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>Creates throw-away users (through the real admin API) for tests that change account state.</summary>
public static class TestUsers
{
    public const string Password = "Test-Passw0rd!2026";

    public static async Task<string> CreateAsync(ApiFactory api, string prefix, string adminEmail = DemoUsers.AcmeHrAdmin, string[]? roles = null)
    {
        var admin = api.CreateApiClient();
        admin.Authorize(await admin.LoginAsync(adminEmail));

        var domain = adminEmail.Split('@')[1];
        var email = $"{prefix}.{Guid.NewGuid():N}@{domain}";
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            email,
            firstName = "Test",
            lastName = prefix,
            roles = roles ?? ["Employee"],
            managerId = (Guid?)null,
            password = Password,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return email;
    }
}
