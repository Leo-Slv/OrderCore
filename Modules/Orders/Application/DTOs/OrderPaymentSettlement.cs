namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>What cancelling an order did to its payment (Orders' own copy of Payments' outcome).</summary>
public enum OrderPaymentSettlement
{
    NothingToSettle,
    Voided,
    Refunded,
}
