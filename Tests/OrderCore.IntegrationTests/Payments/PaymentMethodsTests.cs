using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>The storefront learns what to offer before anyone signs in.</summary>
public sealed class PaymentMethodsTests : IClassFixture<OrderCoreApiFactory>
{
    private readonly OrderCoreApiFactory _factory;

    public PaymentMethodsTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task With_the_fake_provider_card_and_pix_are_offered_without_a_publishable_key()
    {
        var methods = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/payments/methods");

        methods.GetProperty("provider").GetString().Should().Be("Fake");
        methods.GetProperty("methods").EnumerateArray().Select(m => m.GetString()).Should().Equal("Card", "Pix");
        methods.GetProperty("publishableKey").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
