using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OrderCore.IntegrationTests;

/// <summary>
/// One Mailpit container for the whole test run — the same SMTP catcher
/// docker compose runs locally — read back through its HTTP API. Tests
/// share it, so each one looks for its own messages by a recipient address
/// no other test uses.
/// </summary>
public static class TestMailpit
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private static readonly IContainer Container = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/livez")))
        .Build();

    private static readonly Lazy<Task> Started = new(() => Container.StartAsync());

    public static string Host => Container.Hostname;

    public static int Port => Container.GetMappedPublicPort(SmtpPort);

    public static Task StartAsync() => Started.Value;

    /// <summary>A recipient address no other test uses.</summary>
    public static string NewAddress(string prefix = "someone") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Waits until a message to <paramref name="to"/> arrives — with
    /// <paramref name="subject"/>, when given — and returns the newest one
    /// (subject, HTML and text).
    /// </summary>
    public static async Task<MailpitMessage> WaitForMessageAsync(string to, TimeSpan? timeout = null, string? subject = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        using var http = Http();
        while (true)
        {
            var messages = await SearchAsync(http, to, subject);
            if (messages.Count > 0)
            {
                var id = messages[0].GetProperty("ID").GetString();
                var message = await http.GetFromJsonAsync<JsonElement>($"api/v1/message/{id}");
                return new MailpitMessage(
                    message.GetProperty("Subject").GetString()!,
                    message.GetProperty("From").GetProperty("Address").GetString()!,
                    message.GetProperty("HTML").GetString()!,
                    message.GetProperty("Text").GetString()!,
                    message.GetProperty("MessageID").GetString()!);
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No e-mail arrived for {to}.");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>How many messages were delivered to <paramref name="to"/>.</summary>
    public static async Task<int> CountAsync(string to)
    {
        using var http = Http();
        return (await SearchAsync(http, to)).Count;
    }

    private static async Task<IReadOnlyList<JsonElement>> SearchAsync(HttpClient http, string to, string? subject = null)
    {
        var query = subject is null ? $"to:\"{to}\"" : $"to:\"{to}\" subject:\"{subject}\"";
        var result = await http.GetFromJsonAsync<JsonElement>($"api/v1/search?query={Uri.EscapeDataString(query)}");
        return result.GetProperty("messages").EnumerateArray().ToList();
    }

    private static HttpClient Http() =>
        new() { BaseAddress = new Uri($"http://{Container.Hostname}:{Container.GetMappedPublicPort(HttpPort)}/") };
}

public sealed record MailpitMessage(string Subject, string From, string Html, string Text, string MessageId);
