namespace OrderCore.Api.Modules.Orders.Application.DTOs;

public sealed record OrderRefundSummary(
    Guid Id, decimal Amount, string Reason, string Status, DateTimeOffset RequestedAt, DateTimeOffset? ProcessedAt);
