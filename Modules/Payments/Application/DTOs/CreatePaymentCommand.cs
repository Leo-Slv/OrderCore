using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Payments.Application.DTOs;

public sealed record CreatePaymentCommand(Guid OrderId, decimal Amount, string Currency, PaymentMethod Method, string IdempotencyKey);
