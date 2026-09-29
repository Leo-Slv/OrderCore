using FluentAssertions;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.ValueObjects;
using OrderCore.Api.Modules.Orders.Infrastructure.Messaging;
using Xunit;
using DomainEvents = OrderCore.Api.Modules.Orders.Domain.Events;
using IntegrationEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

namespace OrderCore.UnitTests.Orders;

/// <summary>Shipment details are optional, but what is given must hold up.</summary>
public sealed class ShipmentDetailsTests
{
    [Fact]
    public void Nothing_given_is_no_details()
    {
        ShipmentDetails.Create(null, " ", "").Should().BeNull();
    }

    [Fact]
    public void Carrier_code_and_link_are_kept_trimmed()
    {
        var details = ShipmentDetails.Create(" Correios ", " AB123456789BR ", "https://rastreamento.correios.com.br/AB123456789BR");

        details!.Carrier.Should().Be("Correios");
        details.TrackingCode.Should().Be("AB123456789BR");
        details.TrackingUrl.Should().Be("https://rastreamento.correios.com.br/AB123456789BR");
    }

    [Fact]
    public void A_carrier_alone_is_enough()
    {
        ShipmentDetails.Create("Loggi", null, null)!.TrackingCode.Should().BeNull();
    }

    [Fact]
    public void A_tracking_code_needs_its_carrier()
    {
        var act = () => ShipmentDetails.Create(null, "AB123456789BR", null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("rastreamento.correios.com.br/x")]
    [InlineData("ftp://example.com/track")]
    [InlineData("javascript:alert(1)")]
    public void The_link_must_be_an_absolute_web_address(string link)
    {
        var act = () => ShipmentDetails.Create("Correios", null, link);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Values_longer_than_their_columns_are_refused()
    {
        var act = () => ShipmentDetails.Create(new string('c', ShipmentDetails.MaxCarrierLength + 1), null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipping_with_details_keeps_them_and_announces_them()
    {
        var order = Order.Create(Guid.NewGuid(), "BRL", "ORD-2026-000007", DateTimeOffset.UtcNow);
        order.AddItem(Guid.NewGuid(), null, "SKU", "Lamp", null, 10m, 1);
        order.RequestPayment(DateTimeOffset.UtcNow);
        order.Confirm(DateTimeOffset.UtcNow);
        order.StartProcessing(DateTimeOffset.UtcNow);
        order.ClearDomainEvents();
        var details = ShipmentDetails.Create("Correios", "AB123456789BR", "https://correios.example/AB123456789BR");

        order.Ship(DateTimeOffset.UtcNow, details);

        order.Shipment.Should().Be(details);
        var shipped = order.DomainEvents.OfType<DomainEvents.OrderShipped>().Single();
        var announced = (IntegrationEvents.OrderShipped)OrderIntegrationEventTranslator.Translate(order, shipped)!;
        announced.Carrier.Should().Be("Correios");
        announced.TrackingCode.Should().Be("AB123456789BR");
        announced.TrackingUrl.Should().Be("https://correios.example/AB123456789BR");
    }
}
