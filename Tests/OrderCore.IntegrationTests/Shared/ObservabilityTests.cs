using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry.Trace;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// The request's trace, through the real host with the spans exported in
/// memory: a request is one server span with its database work under it,
/// and the caller gets the trace back — in the <c>traceparent</c> header of
/// every response and the <c>traceId</c> of every error.
/// </summary>
public sealed class ObservabilityTests : IClassFixture<ApiDatabase>, IAsyncLifetime
{
    private readonly ApiDatabase _database;
    private readonly List<Activity> _spans = [];
    private WebApplicationFactory<Program> _factory = null!;

    public ObservabilityTests(ApiDatabase database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _factory = _database.CreateFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans))));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task A_request_is_one_server_span_with_its_database_queries_under_it()
    {
        var response = await _factory.CreateClient().GetAsync("/api/catalog/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var traceId = TraceIdOf(response);

        var server = await ServerSpanAsync(traceId);
        server.GetTagItem("http.route").Should().Be("api/catalog/products");
        Spans(traceId).Where(s => s.Source.Name == "Npgsql").Should().NotBeEmpty()
            .And.OnlyContain(s => s.TraceId == server.TraceId, "the queries belong to the request's trace");
    }

    [Fact]
    public async Task An_error_names_the_trace_it_belongs_to()
    {
        var response = await _factory.CreateAdminClient().GetAsync($"/api/admin/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().Should().Be("order_not_found");
        var traceId = problem.GetProperty("traceId").GetString();
        traceId.Should().Be(TraceIdOf(response), "the body and the header name the same trace");
        (await ServerSpanAsync(traceId!)).GetTagItem("http.response.status_code").Should().Be(404);
    }

    [Fact]
    public async Task A_caller_trace_is_continued_rather_than_replaced()
    {
        var callerTrace = ActivityTraceId.CreateRandom();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/categories");
        request.Headers.Add("traceparent", $"00-{callerTrace}-{ActivitySpanId.CreateRandom()}-01");

        var response = await _factory.CreateClient().SendAsync(request);

        TraceIdOf(response).Should().Be(callerTrace.ToString());
    }

    [Fact]
    public async Task One_checkout_is_one_trace_through_the_broker_down_to_the_confirmation_save()
    {
        var admin = await SignInAsAdminAsync(_factory);
        var (customer, _) = await SignUpCustomerAsync(_factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Observed Oven", 90m);
        await _database.SeedStockAsync(product.Id, quantity: 3);
        var traceId = ActivityTraceId.CreateRandom();

        var checkout = await CheckoutAsync(
            customer, addressId, product.Id, quantity: 1, $"traced-{Guid.NewGuid():N}", $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");

        var consumer = await SpanAsync(traceId.ToString(), s =>
            s.Kind == ActivityKind.Consumer && (string?)s.GetTagItem("messaging.destination.name") == "orders.payment-outcomes");
        var producer = Spans(traceId.ToString()).Single(s => s.SpanId == consumer.ParentSpanId);
        producer.Kind.Should().Be(ActivityKind.Producer);
        producer.GetTagItem("messaging.rabbitmq.destination.routing_key").Should().Be("payments.payment-authorized.v1");
        consumer.GetTagItem("messaging.system").Should().Be("rabbitmq");
        consumer.GetTagItem("ordercore.messaging.attempt").Should().Be(1);
        Spans(traceId.ToString()).Should().Contain(
            s => s.Source.Name == "Npgsql" && s.ParentSpanId == consumer.SpanId, "the confirmation is saved inside the consumer span");
    }

    private static string TraceIdOf(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("traceparent", out var values).Should().BeTrue("every response carries its traceparent");
        return values!.Single().Split('-')[1];
    }

    private List<Activity> Spans(string traceId)
    {
        lock (_spans)
        {
            return _spans.Where(s => s.TraceId.ToString() == traceId).ToList();
        }
    }

    /// <summary>The server span is exported when it ends, just after the response went out.</summary>
    private Task<Activity> ServerSpanAsync(string traceId) => SpanAsync(traceId, s => s.Kind == ActivityKind.Server);

    /// <summary>Spans are exported when they end, so a span can show up a moment after what it did.</summary>
    private async Task<Activity> SpanAsync(string traceId, Func<Activity, bool> match)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var span = Spans(traceId).FirstOrDefault(match);
            if (span is not null)
            {
                return span;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No matching span was exported for trace {traceId}.");
    }
}
