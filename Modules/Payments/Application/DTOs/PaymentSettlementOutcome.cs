namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>What cancelling an order did to its payment.</summary>
public enum PaymentSettlementOutcome
{
    /// <summary>No payment, or one that never took money (failed, already voided or refunded).</summary>
    NothingToSettle,

    /// <summary>The authorization was released before capture.</summary>
    Voided,

    /// <summary>The captured amount still held was refunded in full.</summary>
    Refunded,
}
