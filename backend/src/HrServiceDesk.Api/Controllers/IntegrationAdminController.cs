using HrServiceDesk.Api.ErrorHandling;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common;
using HrServiceDesk.Application.Integration;
using HrServiceDesk.Domain.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>HR Admin: API keys and webhook subscriptions of the organisation.</summary>
[Route("api/integrations")]
[Authorize(Policy = Policies.CanAdministerTenant)]
public sealed class IntegrationAdminController : ApiControllerBase
{
    public sealed record CreateKeyRequest(string Name, IReadOnlyList<string> Scopes);

    public sealed record SaveWebhookRequest(string Name, string Url, IReadOnlyList<string> Events, bool IsActive, bool RotateSecret);

    public sealed record CatalogDto(IReadOnlyList<string> Scopes, IReadOnlyList<string> Events);

    /// <summary>Scopes and event types that can be chosen.</summary>
    [HttpGet("catalog")]
    public ActionResult<CatalogDto> Catalog() => Ok(new CatalogDto(ApiScopes.All, WebhookEvents.All));

    [HttpGet("api-keys")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> Keys(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListApiKeysQuery(), cancellationToken));

    /// <summary>Creates a key; the response holds the full key, which is not shown again.</summary>
    [HttpPost("api-keys")]
    [ProducesResponseType<ApiKeyCreatedDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiKeyCreatedDto>> CreateKey(CreateKeyRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new CreateApiKeyCommand(request.Name, request.Scopes), cancellationToken));

    [HttpDelete("api-keys/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Revoke(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new RevokeApiKeyCommand(id), cancellationToken));

    [HttpGet("webhooks")]
    public async Task<ActionResult<IReadOnlyList<WebhookDto>>> Webhooks(CancellationToken cancellationToken) =>
        Ok(await Sender.Send(new ListWebhooksQuery(), cancellationToken));

    /// <summary>Creates a subscription; the response holds its signing secret, shown this once.</summary>
    [HttpPost("webhooks")]
    public async Task<ActionResult<WebhookSavedDto>> CreateWebhook(SaveWebhookRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveWebhookCommand(null, request.Name, request.Url, request.Events, request.IsActive, false), cancellationToken));

    /// <summary>Updates a subscription; with <c>rotateSecret</c> the response holds the new secret.</summary>
    [HttpPut("webhooks/{id:guid}")]
    public async Task<ActionResult<WebhookSavedDto>> UpdateWebhook(Guid id, SaveWebhookRequest request, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new SaveWebhookCommand(id, request.Name, request.Url, request.Events, request.IsActive, request.RotateSecret), cancellationToken));

    [HttpDelete("webhooks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteWebhook(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new DeleteWebhookCommand(id), cancellationToken));

    [HttpGet("webhooks/{id:guid}/deliveries")]
    public async Task<ActionResult<PagedResult<WebhookDeliveryDto>>> Deliveries(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        FromResult(await Sender.Send(new ListWebhookDeliveriesQuery(id, page, pageSize), cancellationToken));

    /// <summary>Sends a "ping" event to check the receiver.</summary>
    [HttpPost("webhooks/{id:guid}/ping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Ping(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new PingWebhookCommand(id), cancellationToken));

    [HttpPost("deliveries/{id:guid}/redeliver")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Redeliver(Guid id, CancellationToken cancellationToken) =>
        FromResult(await Sender.Send(new RedeliverWebhookCommand(id), cancellationToken));
}
