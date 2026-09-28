using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

        using var management = new HttpClient
        {
            BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(ManagementPort)}/api/"),
        };
        management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{Password}")));

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
