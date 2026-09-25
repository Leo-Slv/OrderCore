using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// The backoffice side of Orders: fulfilment with capture on shipping
/// (backoffice decision 1), cancellation that settles the payment and puts
/// stock back (decision 2) — including what happens when a step fails and
/// the admin tries again — payment consumers that tolerate a cancelled
/// order, and the admin list and dashboard.
/// </summary>
public sealed class OrderBackofficeUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakePaymentGateway _payments = new();
    private readonly FakeInventoryService _inventory = new();
    private readonly FakeCustomerDirectory _customers = new();
    private readonly FakeAuditLogService _auditLog = new();

    /// <summary>An order with one line of 2 units at 10, taken to <paramref name="status"/> the way the real flow does.</summary>
    private async Task<Order> StoredOrderAsync(OrderStatus status, Guid? customerId = null, DateTimeOffset? createdAt = null)
    {
        var order = Order.Create(customerId ?? Guid.NewGuid(), "BRL", $"ORD-{Guid.NewGuid():N}"[..12], createdAt ?? Now);
        var productId = Guid.NewGuid();
        order.AddItem(productId, productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 10m, quantity: 2);

        if (status != OrderStatus.Created)
        {
            _inventory.SetAvailable(productId, 2);
            await _inventory.TryReserveOrderItemsAsync(order, CancellationToken.None);
            await _payments.RequestPaymentAsync(order.Id, order.TotalAmount, "BRL", PaymentMethodChoice.Card, order.Id.ToString(), CancellationToken.None);
            order.RequestPayment(Now);
        }

        if (status is OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.Confirm(Now);
            await _inventory.ConsumeReservationsAsync(order.Id, CancellationToken.None);
        }

        if (status is OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.StartProcessing(Now);
        }

        if (status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.Ship(Now);
        }

        if (status == OrderStatus.Delivered)
        {
            order.Deliver(Now);
        }

        order.ClearDomainEvents();
        _orders.Store(order);
        return order;
    }

    private FulfilOrderUseCase Fulfil() => new(_orders, _payments, _auditLog, TimeProvider.System);

    private CancelOrderUseCase Cancel() => new(_orders, _inventory, _payments, _auditLog, TimeProvider.System);

    private ListOrdersUseCase ListOrders() => new(_orders, _customers, _payments);

    [Fact]
    public void Each_fulfilment_step_raises_the_event_the_status_history_records()
    {
        var order = Order.Create(Guid.NewGuid(), "BRL", "ORD-1", Now);
        order.AddItem(Guid.NewGuid(), null, "SKU-1", "Widget", null, 10m, 1);
        order.RequestPayment(Now);
        order.Confirm(Now);
        order.ClearDomainEvents();

        order.StartProcessing(Now);
        order.Ship(Now);
        order.Deliver(Now);

        order.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(OrderProcessingStarted), typeof(OrderShipped), typeof(OrderDelivered));
    }

    [Fact]
    public async Task Moving_an_order_through_fulfilment_captures_the_payment_when_it_ships()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed);

        await Fulfil().StartProcessingAsync(order.Id, CancellationToken.None);
        _payments.Captured.Should().BeEmpty();

        var shipped = await Fulfil().ShipAsync(order.Id, CancellationToken.None);
        _payments.Captured.Should().Equal(order.Id);
        shipped.Order.Status.Should().Be(OrderStatus.Shipped);

        await Fulfil().DeliverAsync(order.Id, CancellationToken.None);
        order.Status.Should().Be(OrderStatus.Delivered);
        _auditLog.Entries.Select(e => e.Action).Should().Equal("OrderProcessingStarted", "OrderShipped", "OrderDelivered");
    }

    [Fact]
    public async Task A_refused_capture_leaves_the_order_processing()
    {
        var order = await StoredOrderAsync(OrderStatus.Processing);
        _payments.FailNextCaptureWith = new ConflictException("payment_capture_failed", "Refused.");

        var act = () => Fulfil().ShipAsync(order.Id, CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_capture_failed");
        order.Status.Should().Be(OrderStatus.Processing);
        _orders.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Shipping_an_order_that_is_not_being_processed_never_captures()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed);

        var act = () => Fulfil().ShipAsync(order.Id, CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.Code.Should().Be("invalid_order_state");
        _payments.Captured.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelling_an_order_awaiting_payment_voids_it_and_releases_its_stock()
    {
        var order = await StoredOrderAsync(OrderStatus.PendingPayment);

        var settlement = await Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "customer asked"), CancellationToken.None);

        settlement.Should().Be(OrderPaymentSettlement.Voided);
        order.Status.Should().Be(OrderStatus.Cancelled);
        _payments.Settlements.Should().Equal((order.Id, "customer asked"));
        _inventory.Reserved.Should().NotContainKey(order.Id);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_order_puts_its_consumed_stock_back()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed);

        await Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "out of stock at the warehouse"), CancellationToken.None);

        _inventory.ReturnedOrders.Should().Equal(order.Id);
        order.Status.Should().Be(OrderStatus.Cancelled);
        var audit = _auditLog.Entries.Single(e => e.Action == "OrderCancelled").Metadata!;
        audit["payment"].Should().Be("Voided");
        audit["returnedUnits"].Should().Be("2");
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public async Task An_order_that_left_cannot_be_cancelled_and_nothing_is_settled(OrderStatus status)
    {
        var order = await StoredOrderAsync(status);

        var act = () => Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "too late"), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.Code.Should().Be("invalid_order_state");
        _payments.Settlements.Should().BeEmpty();
        _inventory.ReturnedOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task A_payment_still_with_the_provider_blocks_the_cancellation_and_changes_nothing()
    {
        var order = await StoredOrderAsync(OrderStatus.PendingPayment);
        _payments.FailNextSettlementWith = new ConflictException("payment_in_progress", "Still processing.");

        var act = () => Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "changed mind"), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("payment_in_progress");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        _inventory.Reserved.Should().ContainKey(order.Id);
    }

    [Fact]
    public async Task A_customer_cancels_their_own_confirmed_order_with_the_same_settlement_as_an_admin()
    {
        var customerId = Guid.NewGuid();
        var order = await StoredOrderAsync(OrderStatus.Confirmed, customerId);

        var settlement = await Cancel().ExecuteAsync(
            new CancelOrderCommand(order.Id, "changed my mind", RequestingCustomerId: customerId), CancellationToken.None);

        settlement.Should().Be(OrderPaymentSettlement.Voided);
        order.Status.Should().Be(OrderStatus.Cancelled);
        _inventory.ReturnedOrders.Should().Equal(order.Id);
        _auditLog.Entries.Single(e => e.Action == "OrderCancelled").Metadata!["cancelledBy"].Should().Be("Customer");
    }

    [Fact]
    public async Task A_customer_cannot_cancel_once_the_store_started_preparing_the_order()
    {
        var customerId = Guid.NewGuid();
        var order = await StoredOrderAsync(OrderStatus.Processing, customerId);

        var act = () => Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "too late", customerId), CancellationToken.None);

        (await act.Should().ThrowAsync<DomainRuleViolationException>()).Which.Code.Should().Be("order_in_fulfilment");
        _payments.Settlements.Should().BeEmpty();
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public async Task A_customer_cannot_cancel_someone_elses_order_and_learns_nothing_about_it()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed, customerId: Guid.NewGuid());

        var act = () => Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "mine?", Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("order_not_found");
        _payments.Settlements.Should().BeEmpty();
    }

    [Fact]
    public async Task A_late_payment_authorization_for_a_cancelled_order_is_skipped()
    {
        var order = await StoredOrderAsync(OrderStatus.PendingPayment);
        await Cancel().ExecuteAsync(new CancelOrderCommand(order.Id, "changed mind"), CancellationToken.None);
        var confirm = new ConfirmOrderUseCase(_orders, _inventory, _auditLog, TimeProvider.System, NullLogger<ConfirmOrderUseCase>.Instance);
        var fail = new MarkOrderPaymentFailedUseCase(
            _orders, _inventory, _auditLog, TimeProvider.System, NullLogger<MarkOrderPaymentFailedUseCase>.Instance);

        await confirm.Invoking(c => c.ExecuteAsync(order.Id, CancellationToken.None)).Should().NotThrowAsync();
        await fail.Invoking(f => f.ExecuteAsync(order.Id, "declined", CancellationToken.None)).Should().NotThrowAsync();

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Internal_notes_are_trimmed_and_blank_clears_them()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed);
        var useCase = new SetOrderInternalNotesUseCase(_orders, _auditLog);

        await useCase.ExecuteAsync(order.Id, "  call before delivering  ", CancellationToken.None);
        order.InternalNotes.Should().Be("call before delivering");

        await useCase.ExecuteAsync(order.Id, "   ", CancellationToken.None);
        order.InternalNotes.Should().BeNull();
    }

    [Fact]
    public async Task Internal_notes_longer_than_the_limit_are_rejected()
    {
        var order = await StoredOrderAsync(OrderStatus.Confirmed);

        var act = () => new SetOrderInternalNotesUseCase(_orders, _auditLog)
            .ExecuteAsync(order.Id, new string('x', Order.MaxInternalNotesLength + 1), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task The_admin_list_shows_each_orders_customer_and_payment_status_newest_first()
    {
        var customerId = Guid.NewGuid();
        _customers.Customers[customerId] = new OrderCustomerSnapshot(customerId, "Ana", "ana@example.com", true);
        var older = await StoredOrderAsync(OrderStatus.Confirmed, customerId, Now.AddHours(-2));
        var newer = await StoredOrderAsync(OrderStatus.Created, customerId, Now.AddHours(-1));

        var page = await ListOrders().ExecuteAsync(new ListOrdersFilter { CustomerId = customerId }, CancellationToken.None);

        page.Items.Select(o => o.Order.Id).Should().Equal(newer.Id, older.Id);
        page.Items[0].PaymentStatus.Should().BeNull();
        page.Items[1].PaymentStatus.Should().Be("Authorized");
        page.Items.Should().OnlyContain(o => o.Customer!.Name == "Ana");
    }

    [Fact]
    public async Task The_admin_list_rejects_a_range_that_ends_before_it_starts()
    {
        var act = () => ListOrders().ExecuteAsync(new ListOrdersFilter { CreatedFrom = Now, CreatedTo = Now.AddDays(-1) }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task The_dashboard_counts_orders_revenue_customers_and_stock_for_the_period()
    {
        await StoredOrderAsync(OrderStatus.Confirmed, createdAt: Now.AddDays(-1));
        await StoredOrderAsync(OrderStatus.Shipped, createdAt: Now.AddDays(-2));
        var cancelled = await StoredOrderAsync(OrderStatus.PendingPayment, createdAt: Now.AddDays(-3));
        await Cancel().ExecuteAsync(new CancelOrderCommand(cancelled.Id, "changed mind"), CancellationToken.None);
        await StoredOrderAsync(OrderStatus.Confirmed, createdAt: Now.AddDays(-60));
        _customers.NewCustomers = 4;
        _inventory.StockAlerts = new StockAlertCounts(LowStock: 2, OutOfStock: 1);
        var dashboard = new GetDashboardUseCase(_orders, _customers, _inventory, ListOrders(), TimeProvider.System);

        var output = await dashboard.ExecuteAsync(Now.AddDays(-30), Now.AddMinutes(1), CancellationToken.None);

        output.OrdersByStatus[OrderStatus.Confirmed].Should().Be(1);
        output.OrdersByStatus[OrderStatus.Shipped].Should().Be(1);
        output.OrdersByStatus[OrderStatus.Cancelled].Should().Be(1);
        output.OrdersByStatus[OrderStatus.Delivered].Should().Be(0);
        output.NewCustomers.Should().Be(4);
        output.Stock.Should().Be(new StockAlertCounts(2, 1));
        output.RecentOrders.Should().HaveCount(4);
    }

    [Fact]
    public async Task The_dashboard_rejects_a_period_that_ends_before_it_starts()
    {
        var dashboard = new GetDashboardUseCase(_orders, _customers, _inventory, ListOrders(), TimeProvider.System);

        var act = () => dashboard.ExecuteAsync(Now, Now.AddDays(-1), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
