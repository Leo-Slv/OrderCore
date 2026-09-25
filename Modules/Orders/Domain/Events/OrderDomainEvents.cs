using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Orders.Domain.Events;

/// <summary>
/// Domain events raised by the <see cref="Entities.Order"/> aggregate
/// (section 18 of the project context). These are dispatched in-process,
/// within the same unit of work. When the system evolves towards
/// cross-module/cross-service communication, a subset of these facts is
/// translated into Integration Events
/// (Modules/Payments/Application/Contracts/IntegrationEvents) published
/// through the Transactional Outbox — the two are deliberately kept as
/// separate types.
/// </summary>
public sealed record OrderCreated(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, Guid CustomerId)
    : IDomainEvent;

public sealed record OrderConfirmed(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId)
    : IDomainEvent;

public sealed record OrderCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, string Reason)
    : IDomainEvent;

public sealed record OrderPaymentFailed(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, string Reason)
    : IDomainEvent;

/// <summary>
/// Stock was reserved and the order moved from Created to PendingPayment.
/// Raised so the status history records this transition too; it is the
/// "Processando pagamento…" step of the tracking timeline.
/// </summary>
public sealed record OrderPaymentRequested(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId)
    : IDomainEvent;

/// <summary>An admin started preparing a confirmed order (backoffice).</summary>
public sealed record OrderProcessingStarted(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId)
    : IDomainEvent;

/// <summary>The order left for delivery; its payment was captured just before (backoffice decision 1).</summary>
public sealed record OrderShipped(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId)
    : IDomainEvent;

public sealed record OrderDelivered(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId)
    : IDomainEvent;
