using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.UseCases;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Modules.Orders.Infrastructure.Adapters;
using OrderCore.Api.Modules.Orders.Infrastructure.EventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.IntegrationEventHandlers;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Outbox;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain.ValueObjects;
using OrderCore.Api.Shared.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The whole point of this feature (see 05-orders.md's "Fluxo de
/// checkout"): create an order → reserve stock → request payment →
/// Payments' outbox "publishes" PaymentAuthorized by calling the real
/// <see cref="IDomainEventDispatcher"/> → Orders' integration event
/// handler confirms the order and consumes the reservation. One Postgres
/// container backs all four modules' DbContexts (Catalog/Inventory/
/// Orders/Payments) — logically separate schemas, one physical database,
/// same as `docker-compose.yml` runs them in practice. Wired by hand
/// (a real <see cref="IServiceProvider"/> just for domain-event handler
/// resolution) rather than through the full ASP.NET host, since the goal
/// is exercising the cross-module flow, not the HTTP layer.
/// </summary>
public sealed class CheckoutFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var catalogDb = new CatalogDbContext(CatalogOptions());
        await catalogDb.Database.MigrateAsync();

        await using var inventoryDb = new InventoryDbContext(InventoryOptions());
        await inventoryDb.Database.MigrateAsync();

        await using var ordersDb = new OrdersDbContext(OrdersOptions());
        await ordersDb.Database.MigrateAsync();

        await using var paymentsDb = new PaymentsDbContext(PaymentsOptions());
        await paymentsDb.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private DbContextOptions<CatalogDbContext> CatalogOptions() =>
        new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

    private DbContextOptions<InventoryDbContext> InventoryOptions() =>
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

    private DbContextOptions<OrdersDbContext> OrdersOptions() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

    private DbContextOptions<PaymentsDbContext> PaymentsOptions() =>
        new DbContextOptionsBuilder<PaymentsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

    [Fact]
    public async Task Requesting_payment_and_publishing_the_outbox_confirms_the_order_and_consumes_the_reservation()
    {
        // Arrange: a category + product (Catalog) and stock for it (Inventory).
        var productId = Guid.NewGuid();

        await using (var catalogDb = new CatalogDbContext(CatalogOptions()))
        {
            var categoryRepository = new EfCategoryRepository(catalogDb);
            var category = Category.Create("Electronics", Slug.Create("electronics"), null, null, DateTimeOffset.UtcNow);
            await categoryRepository.AddAsync(category, CancellationToken.None);
            await categoryRepository.SaveChangesAsync(CancellationToken.None);

            var productRepository = new EfProductRepository(catalogDb);
            var product = Product.Create("SKU-CHECKOUT", "Widget", Slug.Create("widget"), category.Id, 25m, "BRL", DateTimeOffset.UtcNow);
            await productRepository.AddAsync(product, CancellationToken.None);
            await productRepository.SaveChangesAsync(CancellationToken.None);
            productId = product.Id;
        }

        await using (var inventoryDb = new InventoryDbContext(InventoryOptions()))
        {
            var stockRepository = new EfStockItemRepository(inventoryDb);
            var reservationRepository = new EfInventoryReservationRepository(inventoryDb);
            var unitOfWork = new InventoryUnitOfWork(inventoryDb, stockRepository, reservationRepository, NoOpDispatcher());
            await stockRepository.AddAsync(StockItem.Create(productId, 10, null, DateTimeOffset.UtcNow), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        // Act, part 1: create the order and request payment.
        Guid orderId;

        await using (var catalogDb = new CatalogDbContext(CatalogOptions()))
        await using (var inventoryDb = new InventoryDbContext(InventoryOptions()))
        await using (var ordersDb = new OrdersDbContext(OrdersOptions()))
        {
            var productCatalog = new ProductCatalogAdapter(new EfProductRepository(catalogDb));

            var stockRepository = new EfStockItemRepository(inventoryDb);
            var reservationRepository = new EfInventoryReservationRepository(inventoryDb);
            var inventoryUnitOfWork = new InventoryUnitOfWork(inventoryDb, stockRepository, reservationRepository, NoOpDispatcher());
            var inventoryService = new InventoryServiceAdapter(
                new Api.Modules.Inventory.Application.UseCases.ReserveStockUseCase(
                    stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
                new Api.Modules.Inventory.Application.UseCases.ReleaseReservationUseCase(
                    stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
                new Api.Modules.Inventory.Application.UseCases.ConsumeReservationUseCase(
                    stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
                new Api.Modules.Inventory.Application.UseCases.GetStockAvailabilityUseCase(stockRepository),
                reservationRepository);

            var orderRepository = new EfOrderRepository(ordersDb, OrdersDispatcher(ordersDb));
            var orderNumbers = new SequentialOrderNumberGenerator(ordersDb, TimeProvider.System);
            var createOrder = new CreateOrderHandler(orderRepository, productCatalog, orderNumbers, NoOpAuditLog(), TimeProvider.System);

            var order = await createOrder.HandleAsync(
                new Api.Modules.Orders.Application.DTOs.CreateOrderCommand(
                    Guid.NewGuid(), "BRL", [new Api.Modules.Orders.Application.DTOs.CreateOrderItem(productId, 2)]),
                CancellationToken.None);
            orderId = order.OrderId;

            await using var paymentsDb = new PaymentsDbContext(PaymentsOptions());
            var paymentGateway = new PaymentGatewayAdapter(
                CreatePaymentUseCase(paymentsDb), new GetPaymentByOrderIdUseCase(new EfPaymentRepository(paymentsDb)));
            var requestPayment = new RequestOrderPaymentUseCase(orderRepository, inventoryService, paymentGateway, TimeProvider.System);

            await requestPayment.ExecuteAsync(orderId, PaymentMethodChoice.Card, CancellationToken.None);
        }

        // Assert, intermediate: order is PendingPayment, stock is reserved,
        // and Payments enqueued a PaymentAuthorized message (FakePaymentProvider
        // defaults to Success).
        await using (var ordersDb = new OrdersDbContext(OrdersOptions()))
        {
            var order = await new EfOrderRepository(ordersDb, NoOpDispatcher()).GetByIdAsync(orderId, CancellationToken.None);
            order!.Status.Should().Be(OrderStatus.PendingPayment);
        }

        await using (var paymentsDb = new PaymentsDbContext(PaymentsOptions()))
        {
            var pending = await paymentsDb.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();
            pending.Should().ContainSingle(m => m.Type == nameof(PaymentAuthorized));
        }

        // Act, part 2: "publish" the outbox — what OutboxPublisherBackgroundService
        // does on its polling loop, dispatched through a real
        // IDomainEventDispatcher wired to Orders' integration event handlers.
        await using (var ordersDb = new OrdersDbContext(OrdersOptions()))
        await using (var paymentsDb = new PaymentsDbContext(PaymentsOptions()))
        {
            var dispatcher = OrdersDispatcher(ordersDb);

            var pending = await paymentsDb.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();
            foreach (var message in pending)
            {
                var integrationEvent = (PaymentAuthorized)System.Text.Json.JsonSerializer.Deserialize(message.PayloadJson, typeof(PaymentAuthorized))!;
                await dispatcher.DispatchAsync([integrationEvent], CancellationToken.None);
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }

            await paymentsDb.SaveChangesAsync(CancellationToken.None);
        }

        // Assert: the order is Confirmed and the reservation was consumed
        // (stock permanently reduced, not just held).
        await using (var ordersDb = new OrdersDbContext(OrdersOptions()))
        {
            var order = await new EfOrderRepository(ordersDb, NoOpDispatcher()).GetByIdAsync(orderId, CancellationToken.None);
            order!.Status.Should().Be(OrderStatus.Confirmed);
        }

        await using (var inventoryDb = new InventoryDbContext(InventoryOptions()))
        {
            var stockItem = await new EfStockItemRepository(inventoryDb).GetByProductIdAsync(productId, CancellationToken.None);
            stockItem!.QuantityOnHand.Should().Be(8);
            stockItem.QuantityReserved.Should().Be(0);
        }
    }

    private static IDomainEventDispatcher NoOpDispatcher() => new InProcessDomainEventDispatcher(EmptyServiceProvider());

    private static IServiceProvider EmptyServiceProvider() => new ServiceCollection().BuildServiceProvider();

    private static IAuditLogService NoOpAuditLog() => new NoOpAuditLogService();

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(
            string action,
            string entityName,
            Guid? entityId,
            IReadOnlyDictionary<string, string?>? metadata,
            Guid? userId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// A real dispatcher resolving Orders' own domain-event handlers
    /// (OrderStatusHistoryProjector) and its two integration-event handlers
    /// — everything <see cref="EfOrderRepository.SaveChangesAsync"/> and the
    /// manual "publish" step in this test need to actually call, including a
    /// real <see cref="IInventoryService"/> so <c>ConfirmOrderUseCase</c> can
    /// actually consume the reservation against the same Postgres container.
    /// </summary>
    private IDomainEventDispatcher OrdersDispatcher(OrdersDbContext ordersDb)
    {
        var inventoryDb = new InventoryDbContext(InventoryOptions());
        var stockRepository = new EfStockItemRepository(inventoryDb);
        var reservationRepository = new EfInventoryReservationRepository(inventoryDb);
        var inventoryUnitOfWork = new InventoryUnitOfWork(inventoryDb, stockRepository, reservationRepository, NoOpDispatcher());
        var inventoryService = new InventoryServiceAdapter(
            new Api.Modules.Inventory.Application.UseCases.ReserveStockUseCase(
                stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
            new Api.Modules.Inventory.Application.UseCases.ReleaseReservationUseCase(
                stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
            new Api.Modules.Inventory.Application.UseCases.ConsumeReservationUseCase(
                stockRepository, reservationRepository, inventoryUnitOfWork, NoOpAuditLog(), TimeProvider.System),
            new Api.Modules.Inventory.Application.UseCases.GetStockAvailabilityUseCase(stockRepository),
            reservationRepository);

        var services = new ServiceCollection();
        services.AddSingleton(ordersDb);
        services.AddSingleton<OrderStatusHistoryProjector>();
        services.AddSingleton<IDomainEventHandler<OrderCreated>>(sp => sp.GetRequiredService<OrderStatusHistoryProjector>());
        services.AddSingleton<IDomainEventHandler<OrderPaymentRequested>>(sp => sp.GetRequiredService<OrderStatusHistoryProjector>());
        services.AddSingleton<IDomainEventHandler<OrderConfirmed>>(sp => sp.GetRequiredService<OrderStatusHistoryProjector>());
        services.AddSingleton<IDomainEventHandler<OrderCancelled>>(sp => sp.GetRequiredService<OrderStatusHistoryProjector>());
        services.AddSingleton<IDomainEventHandler<OrderPaymentFailed>>(sp => sp.GetRequiredService<OrderStatusHistoryProjector>());

        services.AddSingleton<IOrderRepository>(sp => new EfOrderRepository(ordersDb, NoOpDispatcher()));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInventoryService>(inventoryService);
        services.AddSingleton<IAuditLogService>(NoOpAuditLog());
        services.AddSingleton<ConfirmOrderUseCase>();
        services.AddSingleton<IDomainEventHandler<PaymentAuthorized>, PaymentAuthorizedIntegrationEventHandler>();

        var provider = services.BuildServiceProvider();
        return new InProcessDomainEventDispatcher(provider);
    }

    private static CreatePaymentUseCase CreatePaymentUseCase(PaymentsDbContext paymentsDb)
    {
        var paymentRepository = new EfPaymentRepository(paymentsDb);
        var outbox = new OutboxWriter(paymentsDb);
        var provider = new FakePaymentProvider(Options.Create(new FakePaymentProviderOptions()));
        return new CreatePaymentUseCase(paymentRepository, provider, outbox, NoOpAuditLog(), TimeProvider.System);
    }
}
