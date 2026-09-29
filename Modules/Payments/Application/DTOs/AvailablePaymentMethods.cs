using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>What the storefront may offer at checkout, and what the provider's card form needs.</summary>
public sealed record AvailablePaymentMethods(string Provider, IReadOnlyList<PaymentMethod> Methods, string? PublishableKey);
