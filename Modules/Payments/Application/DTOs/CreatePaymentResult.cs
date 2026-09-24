namespace OrderCore.Api.Modules.Payments.Application.DTOs;

public sealed record CreatePaymentResult(Guid PaymentId, string Status);
