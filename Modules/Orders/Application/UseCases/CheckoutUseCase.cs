using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Single-step checkout: turns a cart into an order that is already
/// awaiting payment. It validates the products, snapshots the chosen
/// addresses, reserves stock and starts payment in one use case, so the
/// storefront no longer chains create → set addresses → request payment
/// itself. The payment outcome still arrives asynchronously through the
/// outbox, exactly as with <see cref="RequestOrderPaymentUseCase"/>.
///
/// Orders, Inventory and Payments each have their own <c>DbContext</c>, so
/// this is a sequence with compensation, not a single transaction:
/// <list type="bullet">
/// <item>everything that can fail validation fails before anything is
/// written;</item>
/// <item>stock is reserved before the order is saved, and released again
/// if that save fails;</item>
/// <item>the order is saved once, going straight from new to PendingPayment;</item>
/// <item>if starting payment fails after that, the order is left
/// PendingPayment with no payment. Retrying with the same idempotency key
/// finds it and requests payment again. Payments allows only one payment
/// per order, so the retry can't charge twice.</item>
/// </list>
/// </summary>
public sealed class CheckoutUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductCatalog _productCatalog;
    private readonly ICustomerDirectory _customerDirectory;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IOrderNumberGenerator _orderNumbers;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CheckoutUseCase(
        IOrderRepository orderRepository,
        IProductCatalog productCatalog,
        ICustomerDirectory customerDirectory,
        IInventoryService inventoryService,
        IPaymentGateway paymentGateway,
        IOrderNumberGenerator orderNumbers,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _productCatalog = productCatalog;
        _customerDirectory = customerDirectory;
        _inventoryService = inventoryService;
        _paymentGateway = paymentGateway;
        _orderNumbers = orderNumbers;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    /// <returns>The id of the order this checkout created, or the one it had already created for this key.</returns>
    public async Task<Guid> ExecuteAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException("An idempotency key is required.", nameof(command));
        }

        var existing = await _orderRepository.FindByCheckoutIdempotencyKeyAsync(command.CustomerId, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await ResumePaymentIfMissingAsync(existing, command.PaymentMethod, cancellationToken);
            return existing.Id;
        }

        if (command.Items.Count == 0)
        {
            throw new DomainRuleViolationException("order_without_items", "An order must contain at least one item.");
        }

        if (command.Items.Any(i => i.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Every item must have a quantity greater than zero.");
        }

        var shippingAddress = await _customerDirectory.GetAddressAsync(command.CustomerId, command.ShippingAddressId, cancellationToken);
        var billingAddress = await _customerDirectory.GetAddressAsync(command.CustomerId, command.BillingAddressId, cancellationToken);

        var products = await LoadPurchasableProductsAsync(command.Items, cancellationToken);
        var currency = products.Values.Select(p => p.Currency).Distinct().Single();

        var total = command.Items.Sum(i => products[i.ProductId].CurrentPrice * i.Quantity);
        if (command.ExpectedTotal is { } expectedTotal && expectedTotal != total)
        {
            throw new ConflictException(
                "price_changed", $"The order total is now {total} {currency}, not the expected {expectedTotal} {currency}.");
        }

        var now = _timeProvider.GetUtcNow();
        var orderNumber = await _orderNumbers.NextAsync(cancellationToken);
        var order = Order.Create(command.CustomerId, currency, orderNumber, now, command.CustomerNotes, command.IdempotencyKey);
        foreach (var item in command.Items)
        {
            var product = products[item.ProductId];
            order.AddItem(product.Id, productVariantId: null, product.Sku, product.Name, product.PrimaryImageUrl, product.CurrentPrice, item.Quantity);
        }

        order.SetAddresses(shippingAddress, billingAddress);

        if (!await _inventoryService.TryReserveOrderItemsAsync(order, cancellationToken))
        {
            throw new ConflictException("insufficient_stock", "There is not enough stock for every item in the cart.");
        }

        order.RequestPayment(now);

        var savedOrder = await SaveReleasingReservationsOnFailureAsync(order, command, cancellationToken);
        if (savedOrder.Id != order.Id)
        {
            return savedOrder.Id;
        }

        await _auditLog.RecordAsync(
            AuditLogActionNames.OrderCreated,
            "Order",
            order.Id,
            new Dictionary<string, string?> { ["customerId"] = order.CustomerId.ToString(), ["totalAmount"] = order.TotalAmount.ToString() },
            userId: null,
            cancellationToken);

        await _paymentGateway.RequestPaymentAsync(
            order.Id, order.TotalAmount, order.Currency, command.PaymentMethod, order.Id.ToString(), cancellationToken);

        return order.Id;
    }

    /// <summary>
    /// Every product must exist and be purchasable, and all must share one
    /// currency. An item listed twice counts once here; <c>Order.AddItem</c>
    /// already adds up the quantities.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, CatalogProductSnapshot>> LoadPurchasableProductsAsync(
        IReadOnlyList<CheckoutItem> items, CancellationToken cancellationToken)
    {
        var products = await _productCatalog.GetManyAsync(items.Select(i => i.ProductId).Distinct().ToList(), cancellationToken);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                throw new NotFoundException("product_not_found", $"Product '{item.ProductId}' was not found.");
            }

            if (!product.IsPurchasable)
            {
                throw new ConflictException("product_unavailable", $"Product '{product.Name}' is no longer available.");
            }
        }

        if (products.Values.Select(p => p.Currency).Distinct().Count() > 1)
        {
            throw new DomainRuleViolationException("mixed_currencies", "Products in the cart are priced in different currencies.");
        }

        return products;
    }

    /// <summary>
    /// Returns the order that ended up saved for this key. That is normally
    /// <paramref name="order"/>. It can be another one when a concurrent
    /// request with the same key saved first: the unique key makes this
    /// save fail, this attempt's reservations are released, and the winner's
    /// order is returned instead of an error.
    /// </summary>
    private async Task<Order> SaveReleasingReservationsOnFailureAsync(
        Order order, CheckoutCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _orderRepository.AddAsync(order, cancellationToken);
            await _orderRepository.SaveChangesAsync(cancellationToken);
            return order;
        }
        catch
        {
            await _inventoryService.ReleaseReservationsAsync(order.Id, cancellationToken);

            var concurrentWinner = await _orderRepository.FindByCheckoutIdempotencyKeyAsync(
                command.CustomerId, command.IdempotencyKey, cancellationToken);
            if (concurrentWinner is not null)
            {
                return concurrentWinner;
            }

            throw;
        }
    }

    private async Task ResumePaymentIfMissingAsync(Order order, PaymentMethodChoice paymentMethod, CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.PendingPayment)
        {
            return;
        }

        if (await _paymentGateway.GetPaymentSummaryAsync(order.Id, cancellationToken) is null)
        {
            await _paymentGateway.RequestPaymentAsync(
                order.Id, order.TotalAmount, order.Currency, paymentMethod, order.Id.ToString(), cancellationToken);
        }
    }
}
