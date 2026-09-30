using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders.Resend;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>
/// The request Resend receives (against a canned HTTP handler, never the real
/// API) and how its answers map to sent, refused for good or retried.
/// </summary>
public sealed class ResendEmailSenderTests
{
    private static readonly OutgoingEmail Email = new(
        Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), "jane@example.com", "Redefinição de senha", "<p>Olá</p>", "Olá");

    [Fact]
    public async Task Sends_the_email_with_the_message_id_as_idempotency_key()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, """{"id":"49a3999c-0ce1-4ea6-ab68-afcd6dc2e794"}""");

        var result = await Sender(handler).SendAsync(Email, CancellationToken.None);

        result.Should().Be(EmailSendResult.Sent);
        var request = handler.Request!;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(new Uri("https://api.resend.com/emails"));
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("re_test_key");
        request.Headers.GetValues("Idempotency-Key").Should().Equal("0f8fad5b-d9cb-469f-a165-70867728950e");

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        body.GetProperty("from").GetString().Should().Be("OrderCore <no-reply@shop.example>");
        body.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal("jane@example.com");
        body.GetProperty("subject").GetString().Should().Be("Redefinição de senha");
        body.GetProperty("html").GetString().Should().Be("<p>Olá</p>");
        body.GetProperty("text").GetString().Should().Be("Olá");
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "validation_error", EmailSendOutcome.Rejected)]
    [InlineData(HttpStatusCode.BadRequest, "validation_error", EmailSendOutcome.Rejected)]
    [InlineData(HttpStatusCode.Forbidden, "validation_error", EmailSendOutcome.Rejected)]
    [InlineData(HttpStatusCode.Forbidden, "suspended_api_key", EmailSendOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.Unauthorized, "missing_api_key", EmailSendOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.Conflict, "concurrent_idempotent_requests", EmailSendOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_exceeded", EmailSendOutcome.TransientFailure)]
    [InlineData(HttpStatusCode.InternalServerError, "application_error", EmailSendOutcome.TransientFailure)]
    public async Task Maps_resend_errors_to_refused_or_retried(HttpStatusCode status, string error, EmailSendOutcome expected)
    {
        var handler = new CannedHandler(status, $$"""{"statusCode":{{(int)status}},"name":"{{error}}","message":"Something about it."}""");

        var result = await Sender(handler).SendAsync(Email, CancellationToken.None);

        result.Outcome.Should().Be(expected);
        result.Error.Should().Be($"Resend {(int)status} {error}: Something about it.");
    }

    [Fact]
    public async Task An_error_without_a_json_body_is_retried()
    {
        var handler = new CannedHandler(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");

        var result = await Sender(handler).SendAsync(Email, CancellationToken.None);

        result.Outcome.Should().Be(EmailSendOutcome.TransientFailure);
        result.Error.Should().Be("Resend 502 unknown: no error details");
    }

    private static ResendEmailSender Sender(CannedHandler handler) => new(
        new SingleClientFactory(handler),
        Options.Create(new ResendOptions { ApiKey = "re_test_key" }),
        Options.Create(new NotificationsOptions { From = "OrderCore <no-reply@shop.example>" }));

    private sealed class CannedHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
