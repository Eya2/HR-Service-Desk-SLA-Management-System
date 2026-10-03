using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

/// <summary>What the fake provider puts in the next ID token, and how to break it on purpose.</summary>
public sealed record FakeSignIn(string Email, string? GivenName = null, string? FamilyName = null)
{
    public string? Audience { get; init; }

    public string? Nonce { get; init; }

    public bool SignWithUnknownKey { get; init; }
}

/// <summary>
/// An in-memory OpenID Connect provider: discovery document, JWKS and a token endpoint that checks the client
/// secret and the PKCE verifier. <see cref="Authorize"/> plays the user signing in at the provider.
/// </summary>
public sealed class FakeIdentityProvider : HttpMessageHandler
{
    public const string ClientId = "hr-desk-test";
    public const string ClientSecret = "test-client-secret";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test-key" };
    private readonly RsaSecurityKey _otherKey = new(RSA.Create(2048)) { KeyId = "test-key" };
    private readonly ConcurrentDictionary<string, (FakeSignIn SignIn, string Nonce, string Challenge, string RedirectUri)> _codes = new();

    public string Authority { get; } = $"https://idp-{Guid.NewGuid():N}.test/tenant/v2.0";

    /// <summary>The user signs in at the provider: returns the redirect back to the app (code and state).</summary>
    public string Authorize(string authorizeUrl, FakeSignIn signIn)
    {
        var query = QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query);
        query["client_id"].ToString().Should().Be(ClientId);
        query["code_challenge_method"].ToString().Should().Be("S256");
        query["scope"].ToString().Should().Contain("openid");

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _codes[code] = (signIn, query["nonce"].ToString(), query["code_challenge"].ToString(), query["redirect_uri"].ToString());
        var redirect = query["redirect_uri"].ToString();
        return QueryHelpers.AddQueryString(new Uri(redirect).PathAndQuery, new Dictionary<string, string?> { ["code"] = code, ["state"] = query["state"].ToString() });
    }

    /// <summary>Registers the provider as the host's OIDC back-channel.</summary>
    public void Configure(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
        services.AddHttpClient("oidc").ConfigurePrimaryHttpMessageHandler(() => this));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsoluteUri;
        if (path == $"{Authority}/.well-known/openid-configuration")
        {
            return Json(new
            {
                issuer = Authority,
                authorization_endpoint = $"{Authority}/authorize",
                token_endpoint = $"{Authority}/token",
                jwks_uri = $"{Authority}/keys",
                response_types_supported = new[] { "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
            });
        }

        if (path == $"{Authority}/keys")
        {
            var parameters = _key.Rsa.ExportParameters(false);
            return Json(new
            {
                keys = new[]
                {
                    new { kty = "RSA", use = "sig", alg = "RS256", kid = _key.KeyId, n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) },
                },
            });
        }

        if (path == $"{Authority}/token" && request.Method == HttpMethod.Post)
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (form["client_id"] != ClientId || form["client_secret"] != ClientSecret)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":\"invalid_client\"}") };
            if (!_codes.TryRemove(form["code"].ToString(), out var grant) || grant.RedirectUri != form["redirect_uri"])
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\"}") };
            var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString())));
            if (challenge != grant.Challenge)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"PKCE\"}") };

            return Json(new { id_token = IdToken(grant.SignIn, grant.Nonce), access_token = "unused", token_type = "Bearer" });
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private string IdToken(FakeSignIn signIn, string nonce)
    {
        var claims = new List<Claim>
        {
            new("sub", $"user-{signIn.Email}"),
            new("email", signIn.Email),
            new("nonce", signIn.Nonce ?? nonce),
        };
        if (signIn.GivenName is not null)
            claims.Add(new Claim("given_name", signIn.GivenName));
        if (signIn.FamilyName is not null)
            claims.Add(new Claim("family_name", signIn.FamilyName));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Authority,
            Audience = signIn.Audience ?? ClientId,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = new SigningCredentials(signIn.SignWithUnknownKey ? _otherKey : _key, SecurityAlgorithms.RsaSha256),
        });
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}

/// <summary>An API host whose OIDC back-channel talks to a <see cref="FakeIdentityProvider"/>.</summary>
public sealed class SsoApiFactory(string connectionString) : ApiFactory(connectionString)
{
    public FakeIdentityProvider Idp { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Idp.Configure(builder);
    }

    /// <summary>A client that does not follow redirects (the test plays the browser).</summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
}
