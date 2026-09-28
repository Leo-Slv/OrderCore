using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace OrderCore.IntegrationTests;

/// <summary>
/// One RabbitMQ container for the whole test run (the API refuses to start
/// without a broker, so every test host needs one, and a container per host
/// would be slow). Each host gets its own virtual host instead — its own
/// exchange and queues — so hosts running at the same time never consume
/// each other's messages.
/// </summary>
public static class TestBroker
{
    public const string Username = "ordercore";
    public const string Password = "ordercore-tests";
    private const int ManagementPort = 15672;

    private static readonly RabbitMqContainer Container = new RabbitMqBuilder("rabbitmq:4-management")
        .WithUsername(Username)
        .WithPassword(Password)
        .WithPortBinding(ManagementPort, true)
        .Build();

    private static readonly Lazy<Task> Started = new(() => Container.StartAsync());

    public static string Host => Container.Hostname;

    public static int Port => Container.GetMappedPublicPort(5672);

    /// <summary>A new, empty virtual host the test user has full rights on.</summary>
    public static async Task<string> CreateVirtualHostAsync()
    {
        await Started.Value;
        var name = $"test-{Guid.NewGuid():N}";

        using var management = Management();

        // The management API answers a little after the broker itself is up.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                (await management.PutAsync($"vhosts/{name}", null)).EnsureSuccessStatusCode();
                break;
            }
            catch (HttpRequestException) when (attempt < 30)
            {
                await Task.Delay(500);
            }
        }

        (await management.PutAsJsonAsync(
                $"permissions/{name}/{Username}", new { configure = ".*", write = ".*", read = ".*" }))
            .EnsureSuccessStatusCode();
        return name;
    }

    /// <summary>
    /// Drops every connection to <paramref name="virtualHost"/> from the
    /// broker's side, as a network failure would; returns how many it closed.
    /// </summary>
    public static async Task<int> CloseConnectionsAsync(string virtualHost)
    {
        using var management = Management();
        var connections = await management.GetFromJsonAsync<List<JsonElement>>($"vhosts/{virtualHost}/connections") ?? [];
        foreach (var connection in connections)
        {
            var name = Uri.EscapeDataString(connection.GetProperty("name").GetString()!);
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"connections/{name}");
            request.Headers.Add("X-Reason", "simulated broker outage");
            (await management.SendAsync(request)).EnsureSuccessStatusCode();
        }

        return connections.Count;
    }

    private static HttpClient Management()
    {
        var management = new HttpClient
        {
            BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(ManagementPort)}/api/"),
        };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}")));
        return management;
    }

    /// <summary>
    /// Publishes an already-published outbox row again into the host's
    /// virtual host, as the broker does on a redelivery (at least once).
    /// </summary>
    public static async Task RedeliverAsync(WebApplicationFactory<Program> factory, OutboxMessage row)
    {
        var broker = factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        await using var connection = await ConnectAsync(broker.VirtualHost);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
        await channel.BasicPublishAsync(
            broker.Exchange,
            IntegrationEventRegistry.RoutingKey(row.Type, row.Version),
            mandatory: false,
            new BasicProperties { MessageId = row.Id.ToString(), DeliveryMode = DeliveryModes.Persistent },
            MessageEnvelope.FromOutbox(row).ToBytes());
    }

    /// <summary>A connection of the test's own, to inspect or publish into a host's virtual host.</summary>
    public static Task<IConnection> ConnectAsync(string virtualHost) =>
        new ConnectionFactory
        {
            HostName = Host,
            Port = Port,
            VirtualHost = virtualHost,
            UserName = Username,
            Password = Password,
        }.CreateConnectionAsync();
}
