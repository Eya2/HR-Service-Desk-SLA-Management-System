using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>HR Admin: the organisation's single sign-on (OpenID Connect, e.g. Microsoft Entra ID).</summary>
[Route("api/settings/sso")]
[Authorize(Policy = Policies.CanAdministerTenant)]
public sealed class SsoSettingsController : ApiControllerBase
{
    public sealed record SaveSsoRequest(
        bool IsEnabled,
        string DisplayName,
        string Authority,
        string? MetadataAddress,
        string ClientId,
        string? ClientSecret,
        IReadOnlyList<string> EmailDomains,
        bool AutoProvision,
        bool PasswordLoginDisabled);

    /// <summary>The settings (never the secret), or 204 when SSO was never set up.</summary>
    [HttpGet]
    [ProducesResponseType<SsoSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<SsoSettingsDto>> Get(CancellationToken cancellationToken) =>
        await Sender.Send(new GetSsoSettingsQuery(), cancellationToken) is { } settings ? Ok(settings) : NoContent();

    [HttpPut]
    [ProducesResponseType<SsoSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SsoSettingsDto>> Save(SaveSsoRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(
            new SaveSsoSettingsCommand(
                request.IsEnabled, request.DisplayName, request.Authority, request.MetadataAddress, request.ClientId,
                request.ClientSecret, request.EmailDomains ?? [], request.AutoProvision, request.PasswordLoginDisabled),
            cancellationToken));

    /// <summary>Reads the provider's discovery document with the saved settings.</summary>
    [HttpPost("test")]
    [ProducesResponseType<SsoProviderInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SsoProviderInfo>> Test(CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new TestSsoCommand(), cancellationToken));
}
