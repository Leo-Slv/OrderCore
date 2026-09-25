namespace OrderCore.Api.Modules.Inventory.Domain.Enums;

public enum ReservationStatus
{
    Reserved = 0,
    Released = 1,
    Consumed = 2,
    Expired = 3,

    /// <summary>
    /// Was consumed, then its units went back on hand because the order
    /// was cancelled. Final.
    /// </summary>
    Returned = 4,
}
