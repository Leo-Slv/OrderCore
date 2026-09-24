namespace OrderCore.Api.Modules.Payments.Application.DTOs;

public sealed record RequestRefundCommand(Guid PaymentId, decimal Amount, string Reason);
