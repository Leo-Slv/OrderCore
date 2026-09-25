using FluentAssertions;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// The read-side use cases the order screens call: details (with payment),
/// a customer's history.
/// </summary>
public sealed class OrderReadUseCaseTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Order StoredOrder(FakeOrderRepository orders, DateTimeOffset createdAt, int quantity = 1)
    {
        var order = Order.Create(CustomerId, "BRL", $"ORD-{createdAt.Ticks}", createdAt);
        order.AddItem(Guid.NewGuid(), productVariantId: null, "SKU-1", "Widget", productImageUrl: null, unitPrice: 10m, quantity);
        orders.Store(order);
        return order;
    }

    [Fact]
    public async Task GetOrderDetails_includes_the_payment_once_it_exists()
    {
        var orders = new FakeOrderRepository();
        var payments = new FakePaymentGateway();
        var order = StoredOrder(orders, DateTimeOffset.UtcNow);
        var useCase = new GetOrderDetailsUseCase(orders, payments);

        (await useCase.ExecuteAsync(order.Id, requestingCustomerId: null, CancellationToken.None)).Payment.Should().BeNull();

        await payments.RequestPaymentAsync(order.Id, 10m, "BRL", PaymentMethodChoice.Card, order.Id.ToString(), CancellationToken.None);

        var details = await useCase.ExecuteAsync(order.Id, requestingCustomerId: null, CancellationToken.None);
        details.Order.Id.Should().Be(order.Id);
        details.Payment!.Method.Should().Be(PaymentMethodChoice.Card);
    }

    [Fact]
    public async Task GetOrderDetails_for_unknown_order_throws_order_not_found()
    {
        var act = () => new GetOrderDetailsUseCase(new FakeOrderRepository(), new FakePaymentGateway())
            .ExecuteAsync(Guid.NewGuid(), requestingCustomerId: null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "order_not_found");
    }

    [Fact]
    public async Task A_customer_sees_their_own_order_and_not_someone_elses()
    {
        var orders = new FakeOrderRepository();
        var order = StoredOrder(orders, DateTimeOffset.UtcNow);
        var details = new GetOrderDetailsUseCase(orders, new FakePaymentGateway());
        var history = new GetOrderStatusHistoryUseCase(orders, new EmptyStatusHistoryReader());

        (await details.ExecuteAsync(order.Id, CustomerId, CancellationToken.None)).Order.Id.Should().Be(order.Id);

        var otherCustomer = Guid.NewGuid();
        var readDetails = () => details.ExecuteAsync(order.Id, otherCustomer, CancellationToken.None);
        var readHistory = () => history.ExecuteAsync(order.Id, otherCustomer, CancellationToken.None);

        await readDetails.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "order_not_found");
        await readHistory.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "order_not_found");
    }

    [Fact]
    public async Task ListCustomerOrders_pages_newest_first_and_counts_units()
    {
        var orders = new FakeOrderRepository();
        var now = DateTimeOffset.UtcNow;
        var oldest = StoredOrder(orders, now.AddDays(-2));
        var middle = StoredOrder(orders, now.AddDays(-1), quantity: 3);
        StoredOrder(orders, now);

        var page = await new ListCustomerOrdersUseCase(orders).ExecuteAsync(
            new ListCustomerOrdersInput { CustomerId = CustomerId, Page = 2, PageSize = 2 }, CancellationToken.None);

        page.TotalItems.Should().Be(3);
        page.TotalPages.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Id.Should().Be(oldest.Id);

        var firstPage = await new ListCustomerOrdersUseCase(orders).ExecuteAsync(
            new ListCustomerOrdersInput { CustomerId = CustomerId, Page = 1, PageSize = 2 }, CancellationToken.None);
        firstPage.Items.Last().Id.Should().Be(middle.Id);
        firstPage.Items.Last().ItemCount.Should().Be(3);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, ListCustomerOrdersInput.MaximumPageSize + 1)]
    public async Task ListCustomerOrders_rejects_out_of_range_paging(int page, int pageSize)
    {
        var act = () => new ListCustomerOrdersUseCase(new FakeOrderRepository()).ExecuteAsync(
            new ListCustomerOrdersInput { CustomerId = CustomerId, Page = page, PageSize = pageSize }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private sealed class EmptyStatusHistoryReader : IOrderStatusHistoryReader
    {
        public Task<IReadOnlyList<OrderStatusHistoryEntry>> ListAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderStatusHistoryEntry>>([]);
    }
}
