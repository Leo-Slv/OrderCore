namespace OrderCore.Api.Modules.Payments.Application.DTOs;

public sealed record CreatePaymentCommand(Guid OrderId, decimal Amount, string Currency, string IdempotencyKey);
