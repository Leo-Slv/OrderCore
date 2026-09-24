using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// An order plus its payment, if one has been requested yet. This is
/// everything the tracking screen polls for.
/// </summary>
public sealed record OrderDetailsOutput(Order Order, OrderPaymentSummary? Payment);
