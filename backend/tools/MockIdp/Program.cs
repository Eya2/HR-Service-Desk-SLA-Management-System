using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MockIdp;

// A stand-in for Microsoft Entra ID in the demo: an OpenID Connect provider with an account picker.
// The browser uses the public URL; the desk's API reads the discovery document, keys and token
// endpoint through the internal URL (both are configured below).
var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("Idp").Get<IdpSettings>() ?? new IdpSettings();
var app = builder.Build();

using var rsa = RSA.Create(2048);
var keyId = Convert.ToHexString(SHA256.HashData(rsa.ExportRSAPublicKey()))[..16].ToLowerInvariant();
var codes = new ConcurrentDictionary<string, Grant>();

app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
{
    ["issuer"] = settings.PublicUrl,
    ["authorization_endpoint"] = $"{settings.PublicUrl}/authorize",
    ["token_endpoint"] = $"{settings.InternalUrl}/token",
    ["jwks_uri"] = $"{settings.InternalUrl}/keys",
    ["response_types_supported"] = new List<string> { "code" },
    ["subject_types_supported"] = new List<string> { "public" },
    ["id_token_signing_alg_values_supported"] = new List<string> { "RS256" },
    ["scopes_supported"] = new List<string> { "openid", "profile", "email" },
    ["code_challenge_methods_supported"] = new List<string> { "S256" },
}));

app.MapGet("/keys", () =>
{
    var p = rsa.ExportParameters(false);
    return Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = keyId, n = Jwt.B64(p.Modulus!), e = Jwt.B64(p.Exponent!) } } });
});

// The account picker (what Microsoft's sign-in page would do).
app.MapGet("/authorize", (HttpRequest request) =>
{
    var q = request.Query;
    if (q["client_id"] != settings.ClientId)
        return Results.BadRequest("Unknown client_id.");
    var hint = q["login_hint"].ToString();
    return Results.Content(Pages.Picker(settings, request.QueryString.Value ?? string.Empty, hint), "text/html; charset=utf-8");
});

app.MapPost("/authorize", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var account = settings.Accounts.FirstOrDefault(a => a.Email == form["email"]);
    var redirectUri = form["redirect_uri"].ToString();
    if (account is null || string.IsNullOrEmpty(redirectUri))
        return Results.BadRequest("Unknown account.");

    var code = Jwt.B64(RandomNumberGenerator.GetBytes(24));
    codes[code] = new Grant(account, form["nonce"]!, form["code_challenge"]!, redirectUri, DateTimeOffset.UtcNow.AddMinutes(2));
    var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
    return Results.Redirect($"{redirectUri}{separator}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(form["state"]!)}");
});

app.MapPost("/token", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    if (form["client_id"] != settings.ClientId || form["client_secret"] != settings.ClientSecret)
        return Results.Json(new { error = "invalid_client" }, statusCode: 401);
    if (!codes.TryRemove(form["code"].ToString(), out var grant) || grant.ExpiresAt < DateTimeOffset.UtcNow || grant.RedirectUri != form["redirect_uri"])
        return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
    if (Jwt.B64(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))) != grant.Challenge)
        return Results.Json(new { error = "invalid_grant", error_description = "PKCE verification failed" }, statusCode: 400);

    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var idToken = Jwt.Sign(rsa, keyId, new Dictionary<string, object>
    {
        ["iss"] = settings.PublicUrl,
        ["aud"] = settings.ClientId,
        ["sub"] = Jwt.B64(SHA256.HashData(Encoding.UTF8.GetBytes(grant.Account.Email)))[..22],
        ["email"] = grant.Account.Email,
        ["preferred_username"] = grant.Account.Email,
        ["name"] = $"{grant.Account.FirstName} {grant.Account.LastName}",
        ["given_name"] = grant.Account.FirstName,
        ["family_name"] = grant.Account.LastName,
        ["nonce"] = grant.Nonce,
        ["iat"] = now,
        ["nbf"] = now,
        ["exp"] = now + 300,
    });
    return Results.Json(new { id_token = idToken, access_token = Jwt.B64(RandomNumberGenerator.GetBytes(16)), token_type = "Bearer", expires_in = 300 });
});

app.MapGet("/health", () => "ok");
await app.RunAsync();

namespace MockIdp
{
    internal sealed record Account(string Email, string FirstName, string LastName, string Note);

    internal sealed record Grant(Account Account, string Nonce, string Challenge, string RedirectUri, DateTimeOffset ExpiresAt);

    internal sealed class IdpSettings
    {
        public string PublicUrl { get; set; } = "http://localhost:8095";

        public string InternalUrl { get; set; } = "http://mock-idp:8080";

        public string ClientId { get; set; } = "hr-service-desk";

        public string ClientSecret { get; set; } = string.Empty;

        public List<Account> Accounts { get; set; } =
        [
            new("amira.bensalah@acme.example", "Amira", "Ben Salah", "Employee"),
            new("youssef.haddad@acme.example", "Youssef", "Haddad", "Manager"),
            new("leila.mansour@acme.example", "Leila", "Mansour", "HR Officer"),
            new("nadia.jaziri@acme.example", "Nadia", "Jaziri", "HR Admin"),
            new("nour.benali@acme.example", "Nour", "Ben Ali", "New joiner (no account yet)"),
        ];
    }

    internal static class Jwt
    {
        public static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string Sign(RSA rsa, string keyId, Dictionary<string, object> claims)
        {
            var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = keyId }));
            var payload = B64(JsonSerializer.SerializeToUtf8Bytes(claims));
            var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{header}.{payload}.{B64(signature)}";
        }
    }

    internal static class Pages
    {
        public static string Picker(IdpSettings settings, string query, string hint)
        {
            var parameters = System.Web.HttpUtility.ParseQueryString(query);
            var hidden = new StringBuilder();
            foreach (var name in new[] { "redirect_uri", "state", "nonce", "code_challenge" })
                hidden.Append(System.Globalization.CultureInfo.InvariantCulture, $"<input type=\"hidden\" name=\"{name}\" value=\"{WebUtility.HtmlEncode(parameters[name])}\">");

            var accounts = new StringBuilder();
            foreach (var a in settings.Accounts.OrderByDescending(a => a.Email == hint))
            {
                var initials = $"{a.FirstName[0]}{a.LastName[0]}";
                accounts.Append(System.Globalization.CultureInfo.InvariantCulture, $"""
                    <button name="email" value="{WebUtility.HtmlEncode(a.Email)}" class="account{(a.Email == hint ? " hint" : "")}">
                      <span class="avatar">{initials}</span>
                      <span class="who"><strong>{WebUtility.HtmlEncode(a.FirstName)} {WebUtility.HtmlEncode(a.LastName)}</strong><small>{WebUtility.HtmlEncode(a.Email)} · {WebUtility.HtmlEncode(a.Note)}</small></span>
                    </button>
                    """);
            }

            return $$"""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>Sign in to your account</title>
                <style>
                body{margin:0;min-height:100vh;display:grid;place-items:center;background:#f2f2f2;font:15px "Segoe UI",system-ui,sans-serif;color:#1b1b1b}
                .card{width:440px;max-width:calc(100vw - 32px);background:#fff;box-shadow:0 2px 6px rgba(0,0,0,.2);padding:44px}
                .logo{display:flex;gap:2px;flex-wrap:wrap;width:22px;margin-bottom:16px}.logo i{width:10px;height:10px;display:block}
                h1{font-size:24px;font-weight:600;margin:0 0 4px}p{margin:0 0 20px;color:#555}
                .account{display:flex;align-items:center;gap:12px;width:100%;padding:12px;border:0;border-top:1px solid #eee;background:#fff;text-align:left;cursor:pointer;font:inherit}
                .account:hover,.account.hint{background:#f3f6fc}.avatar{width:40px;height:40px;border-radius:50%;display:grid;place-items:center;background:#0067b8;color:#fff;font-weight:600}
                .who{display:grid}small{color:#666}.note{margin-top:20px;font-size:12px;color:#777}
                </style></head><body><form class="card" method="post" action="/authorize">
                <div class="logo"><i style="background:#f25022"></i><i style="background:#7fba00"></i><i style="background:#00a4ef"></i><i style="background:#ffb900"></i></div>
                <h1>Pick an account</h1><p>to continue to HR Service Desk</p>
                {{hidden}}{{accounts}}
                <p class="note">Demo identity provider — stands in for Microsoft Entra ID.</p>
                </form></body></html>
                """;
        }
    }
}
