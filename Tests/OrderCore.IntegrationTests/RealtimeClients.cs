using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;
using OrderCore.Api.Modules.Orders.Presentation.Realtime;

namespace OrderCore.IntegrationTests;

/// <summary>
/// SignalR connections to the test host, the way a browser makes them:
/// negotiate over HTTP, then a WebSocket carrying the access token in the
/// <c>access_token</c> query string (browsers can't set the header there).
/// </summary>
public static class RealtimeClients
{
    public static string AccessTokenFor(this WebApplicationFactory<Program> factory, UserAccount account, DateTimeOffset? issuedAt = null) =>
        new JwtAccessTokenIssuer(factory.Services.GetRequiredService<IOptions<JwtOptions>>())
            .Issue(account, issuedAt ?? DateTimeOffset.UtcNow).Value;

    public static string AdminToken(this WebApplicationFactory<Program> factory, DateTimeOffset? issuedAt = null) =>
        factory.AccessTokenFor(UserAccount.CreateAdmin("admin@example.com", "not-a-real-hash", DateTimeOffset.UtcNow), issuedAt);

    public static string CustomerToken(this WebApplicationFactory<Program> factory, Guid customerId)
    {
        var account = UserAccount.CreateCustomer("jane@example.com", "not-a-real-hash", DateTimeOffset.UtcNow);
        account.LinkCustomer(customerId);
        return factory.AccessTokenFor(account);
    }

    /// <summary>A started connection to the order updates hub, or the exception starting it threw.</summary>
    public static async Task<HubConnection> ConnectToOrderUpdatesAsync(this WebApplicationFactory<Program> factory, string? accessToken)
    {
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, OrderUpdatesHub.Path.TrimStart('/')), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.WebSockets;
                if (accessToken is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }

                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var uri = accessToken is null
                        ? context.Uri
                        : new Uri($"{context.Uri}{(context.Uri.Query.Length > 0 ? "&" : "?")}access_token={Uri.EscapeDataString(accessToken)}");
                    return await server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                };
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
