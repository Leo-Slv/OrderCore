namespace OrderCore.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// Payment has its own state machine, independent from
/// <see cref="Orders.Domain.Enums.OrderStatus"/> (section 16). It is valid
/// for Order = PendingPayment while Payment = Authorized during a
/// transition; the two are kept eventually consistent as the system
/// evolves towards asynchronous communication.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Authorized = 2,
    Captured = 3,
    Failed = 4,
    Refunded = 5,
}
