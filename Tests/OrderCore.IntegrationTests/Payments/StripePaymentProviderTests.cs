using System.Net;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;
using Stripe;
using Xunit;
using PaymentMethod = OrderCore.Api.Modules.Payments.Domain.Enums.PaymentMethod;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// The Stripe provider against <c>stripe-mock</c> — Stripe's official API
/// mock, in a container — so no Stripe account or network is needed: what
/// each call sends (amounts in cents, manual capture, card only, the ids in
/// the metadata, one idempotency key per payment and operation) and how
/// Stripe's answers are read.
/// </summary>
public sealed class StripePaymentProviderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly IContainer _stripeMock = new ContainerBuilder("stripe/stripe-mock:latest")
        .WithPortBinding(12111, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(12111))
        .Build();

    private readonly RecordingHandler _recorder = new();

    public Task InitializeAsync() => _stripeMock.StartAsync();

    public async Task DisposeAsync() => await _stripeMock.DisposeAsync();

    private StripePaymentProvider Provider()
    {
        var httpClients = new ServiceCollection()
            .AddHttpClient(StripePaymentProvider.HttpClientName)
            .AddHttpMessageHandler(() => _recorder)
            .Services.BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
        return new StripePaymentProvider(
            Options.Create(new StripeOptions
            {
                SecretKey = "sk_test_ordercore",
                PublishableKey = "pk_test_ordercore",
                ApiBase = $"http://{_stripeMock.Hostname}:{_stripeMock.GetMappedPublicPort(12111)}",
                MaxNetworkRetries = 0,
            }),
            httpClients);
    }

    private static Payment NewPayment(decimal amount = 59.90m) =>
        Payment.Create(Guid.NewGuid(), amount, "BRL", PaymentMethod.Card, $"idem-{Guid.NewGuid():N}", "Stripe", null, Now);

    private static Payment Authorized()
    {
        var payment = NewPayment();
        payment.MarkProcessing();
        payment.Authorize("pi_123", Now);
        return payment;
    }

    [Fact]
    public void It_takes_cards_only_and_hands_out_the_publishable_key()
    {
        var info = Provider().Info;

        info.Name.Should().Be("Stripe");
        info.SupportedMethods.Should().Equal(PaymentMethod.Card);
        info.PublishableKey.Should().Be("pk_test_ordercore");
    }

    [Fact]
    public async Task Authorizing_creates_a_manual_capture_card_intent_and_waits_for_the_buyer()
    {
        var payment = NewPayment(59.90m);

        var result = await Provider().AuthorizeAsync(payment, CancellationToken.None);

        result.RequiresBuyer.Should().BeTrue();
        result.ProviderReference.Should().StartWith("pi_");
        result.ClientSecret.Should().NotBeNullOrEmpty();
        var request = _recorder.Single("POST", "/v1/payment_intents");
        request.Form.Should().Contain("amount", "5990")
            .And.Contain("currency", "brl")
            .And.Contain("capture_method", "manual")
            .And.Contain("payment_method_types[0]", "card")
            .And.Contain("metadata[order_id]", payment.OrderId.ToString())
            .And.Contain("metadata[payment_id]", payment.Id.ToString());
        request.IdempotencyKey.Should().Be($"ordercore-{payment.Id:N}-authorize");
    }

    [Fact]
    public async Task Repeating_an_authorization_sends_the_same_idempotency_key()
    {
        var payment = NewPayment();
        var provider = Provider();

        await provider.AuthorizeAsync(payment, CancellationToken.None);
        await provider.AuthorizeAsync(payment, CancellationToken.None);

        _recorder.Requests.Where(r => r.Path == "/v1/payment_intents").Select(r => r.IdempotencyKey).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Capture_and_void_act_on_the_intent_each_with_its_own_key()
    {
        var payment = Authorized();
        var provider = Provider();

        (await provider.CaptureAsync(payment, CancellationToken.None)).Succeeded.Should().BeTrue();
        (await provider.VoidAsync(payment, CancellationToken.None)).Succeeded.Should().BeTrue();

        _recorder.Single("POST", "/v1/payment_intents/pi_123/capture").IdempotencyKey.Should().Be($"ordercore-{payment.Id:N}-capture");
        _recorder.Single("POST", "/v1/payment_intents/pi_123/cancel").IdempotencyKey.Should().Be($"ordercore-{payment.Id:N}-cancel");
    }

    [Fact]
    public async Task The_client_secret_of_a_waiting_payment_is_asked_of_stripe_again()
    {
        var payment = NewPayment();
        payment.MarkProcessing();
        payment.AwaitBuyer("pi_123", Now);

        var secret = await Provider().GetClientSecretAsync(payment, CancellationToken.None);

        secret.Should().NotBeNullOrEmpty("stripe-mock's intent still needs a payment method");
        _recorder.Single("GET", "/v1/payment_intents/pi_123");
    }

    [Fact]
    public async Task The_capture_deadline_is_read_from_the_intents_card_charge()
    {
        await Provider().GetCaptureDeadlineAsync("pi_123", CancellationToken.None);

        _recorder.Requests.Should().ContainSingle(r => r.Method == "GET" && r.Path == "/v1/payment_intents/pi_123");
        _recorder.RawQueries.Should().ContainSingle().Which.Should().Contain("expand[0]=latest_charge");
    }

    [Fact]
    public async Task The_state_of_a_payment_is_the_intent_as_stripe_has_it()
    {
        var state = await Provider().GetStateAsync(Authorized(), CancellationToken.None);

        // stripe-mock's fixture intent still needs a payment method.
        state.Status.Should().Be(PaymentProviderStatus.WaitingForBuyer);
        _recorder.Requests.Should().ContainSingle(r => r.Method == "GET" && r.Path == "/v1/payment_intents/pi_123");
    }

    [Fact]
    public async Task A_refund_sends_its_amount_keyed_by_the_refund()
    {
        var payment = Authorized();
        payment.Capture(Now);
        var refund = payment.RequestRefund(20m, "damaged", Now);

        (await Provider().RefundAsync(payment, CancellationToken.None)).Succeeded.Should().BeTrue();

        var request = _recorder.Single("POST", "/v1/refunds");
        request.Form.Should().Contain("payment_intent", "pi_123").And.Contain("amount", "2000");
        request.IdempotencyKey.Should().Be($"ordercore-refund-{refund.Id:N}");
    }

    [Fact]
    public async Task A_declined_card_is_a_refusal_with_stripes_decline_code()
    {
        _recorder.Respond = (HttpStatusCode.PaymentRequired,
            """{"error":{"type":"card_error","code":"card_declined","decline_code":"insufficient_funds","message":"Your card has insufficient funds."}}""");

        var result = await Provider().CaptureAsync(Authorized(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task Stripe_failing_is_thrown_not_taken_for_a_refusal()
    {
        _recorder.Respond = (HttpStatusCode.InternalServerError, """{"error":{"type":"api_error","message":"Something went wrong."}}""");

        var act = () => Provider().CaptureAsync(Authorized(), CancellationToken.None);

        await act.Should().ThrowAsync<StripeException>();
    }

    [Theory]
    [InlineData(59.90, "BRL", 5990)]
    [InlineData(0.01, "USD", 1)]
    [InlineData(500, "JPY", 500)]
    public void Amounts_go_in_the_currencys_smallest_unit(decimal amount, string currency, long expected)
    {
        StripePaymentProvider.ToMinorUnits(amount, currency).Should().Be(expected);
    }

    /// <summary>Records what the SDK sends, then forwards it to stripe-mock — or answers with <see cref="Respond"/>.</summary>
    private sealed class RecordingHandler : DelegatingHandler
    {
        public List<(string Method, string Path, Dictionary<string, string> Form, string? IdempotencyKey)> Requests { get; } = [];

        public (HttpStatusCode Status, string Body)? Respond { get; set; }

        /// <summary>The decoded query strings of GET requests (expansions travel there).</summary>
        public List<string> RawQueries { get; } = [];

        public (string Method, string Path, Dictionary<string, string> Form, string? IdempotencyKey) Single(string method, string path) =>
            Requests.Should().ContainSingle(r => r.Method == method && r.Path == path).Subject;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : string.Empty);
            var key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, form, key));
            if (!string.IsNullOrEmpty(request.RequestUri.Query))
            {
                RawQueries.Add(Uri.UnescapeDataString(request.RequestUri.Query));
            }

            if (Respond is { } canned)
            {
                return new HttpResponseMessage(canned.Status) { Content = new StringContent(canned.Body, Encoding.UTF8, "application/json") };
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
