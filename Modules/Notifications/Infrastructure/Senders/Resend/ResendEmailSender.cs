using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Senders.Resend;

/// <summary>
/// Sends through Resend's HTTP API (<c>POST /emails</c>), with the message id
/// as the idempotency key: a retry within 24 hours never sends twice. What
/// Resend refuses for this message (<c>400</c>/<c>422</c>, or <c>403
/// validation_error</c> for a recipient it won't deliver to) fails it at
/// once; anything else — rate limits, a bad key, Resend down — is retried.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    public const string HttpClientName = "Resend";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResendOptions _resend;
    private readonly NotificationsOptions _notifications;

    public ResendEmailSender(
        IHttpClientFactory httpClientFactory, IOptions<ResendOptions> resend, IOptions<NotificationsOptions> notifications)
    {
        _httpClientFactory = httpClientFactory;
        _resend = resend.Value;
        _notifications = notifications.Value;
    }

    public string Name => "resend";

    public async Task<EmailSendResult> SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = _resend.ApiBase;
        client.Timeout = _resend.RequestTimeout;

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendEmail(
                _notifications.From!, [email.To], email.Subject, email.HtmlBody, email.TextBody)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resend.ApiKey);
        request.Headers.Add("Idempotency-Key", email.MessageId.ToString());

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return EmailSendResult.Sent;
        }

        var error = await ReadErrorAsync(response, cancellationToken);
        var description = $"Resend {(int)response.StatusCode} {error.Name}: {error.Message}";
        var refused = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            || (response.StatusCode == HttpStatusCode.Forbidden && error.Name == "validation_error");
        return refused ? EmailSendResult.Rejected(description) : EmailSendResult.Transient(description);
    }

    private static async Task<ResendError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ResendError>(cancellationToken) ?? ResendError.Unknown;
        }
        catch (JsonException)
        {
            return ResendError.Unknown;
        }
    }

    private sealed record ResendEmail(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ResendError(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("message")] string? Message)
    {
        public static readonly ResendError Unknown = new("unknown", "no error details");
    }
}
