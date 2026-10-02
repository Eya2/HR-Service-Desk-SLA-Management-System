using System.Net;
using System.Net.Sockets;
using System.Text;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Integration;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.Infrastructure.Integration;

/// <summary>
/// POSTs signed deliveries. Redirects are not followed, the timeout is short, and unless allowed the connection
/// is refused to private, loopback and link-local addresses — checked on the address actually connected to,
/// so DNS tricks cannot reach internal services.
/// </summary>
internal sealed class HttpWebhookSender(IHttpClientFactory httpClients, TimeProvider clock) : IWebhookSender
{
    public const string ClientName = "webhooks";

    public async Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.Payload, Encoding.UTF8, "application/json"),
        };
        message.Headers.Add(WebhookSignature.SignatureHeader, WebhookSignature.Compute(request.Secret, clock.GetUtcNow().ToUnixTimeSeconds(), request.Payload));
        message.Headers.Add(WebhookSignature.EventHeader, request.EventType);
        message.Headers.Add(WebhookSignature.DeliveryHeader, request.DeliveryId.ToString());

        try
        {
            using var response = await httpClients.CreateClient(ClientName).SendAsync(message, cancellationToken);
            return response.IsSuccessStatusCode
                ? new WebhookResponse(true, (int)response.StatusCode, null)
                : new WebhookResponse(false, (int)response.StatusCode, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (HttpRequestException ex)
        {
            return new WebhookResponse(false, null, ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookResponse(false, null, "The receiver did not answer in time.");
        }
    }

    internal static SocketsHttpHandler CreateHandler(IntegrationOptions options) => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            var allowed = addresses.Where(a => options.AllowPrivateNetworks || IsPublic(a)).ToArray();
            if (allowed.Length == 0)
                throw new HttpRequestException($"{context.DnsEndPoint.Host} resolves to a private or reserved address, which webhooks may not call.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    /// <summary>False for loopback, private (RFC 1918, ULA), link-local, CGNAT, unspecified and multicast addresses.</summary>
    internal static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 0
            || b[0] >= 224);
    }
}
