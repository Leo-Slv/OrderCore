namespace OrderCore.Api.Modules.AuditLogs.Application.Constants;

/// <summary>
/// Names of the actions recorded through <see cref="Services.IAuditLogService"/>.
/// Kept as plain string constants (like CourseCore's
/// <c>AuditLogActionNames</c>) rather than an enum so new modules can add
/// their own action names without every caller needing to reference a
/// single shared enum type. This list reflects OrderCore's actual domain
/// events and state transitions (sections 9-16 of the project context) —
/// grow it as use cases start calling <c>RecordAsync</c>, not ahead of
/// need (section 38).
/// </summary>
public static class AuditLogActionNames
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderCancelled = "OrderCancelled";
    public const string OrderPaymentFailed = "OrderPaymentFailed";

    public const string PaymentAuthorized = "PaymentAuthorized";
    public const string PaymentCaptured = "PaymentCaptured";
    public const string PaymentFailed = "PaymentFailed";
    public const string PaymentRefunded = "PaymentRefunded";
    public const string PaymentVoided = "PaymentVoided";

    public const string InventoryReserved = "InventoryReserved";
    public const string InventoryReleased = "InventoryReleased";
    public const string InventoryConsumed = "InventoryConsumed";
    public const string InventoryExpired = "InventoryExpired";

    public const string ProductCreated = "ProductCreated";
    public const string ProductPriceChanged = "ProductPriceChanged";
    public const string ProductPublished = "ProductPublished";

    public const string CustomerCreated = "CustomerCreated";
    public const string UserAccountCreated = "UserAccountCreated";
    public const string RefreshTokenReuseDetected = "RefreshTokenReuseDetected";
}
