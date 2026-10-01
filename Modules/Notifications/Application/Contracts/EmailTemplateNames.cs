namespace OrderCore.Api.Modules.Notifications.Application.Contracts;

/// <summary>The templates Notifications knows (<c>Infrastructure/Templates/&lt;name&gt;.html|.txt</c>).</summary>
public static class EmailTemplateNames
{
    /// <summary>Values: <c>greeting</c>, <c>link</c>, <c>validFor</c>.</summary>
    public const string PasswordReset = "password-reset";

    /// <summary>Values: <c>greeting</c>, <c>link</c>, <c>validFor</c>.</summary>
    public const string EmailConfirmation = "email-confirmation";

    /// <summary>Values: <c>greeting</c>, <c>orderNumber</c>, <c>total</c>.</summary>
    public const string OrderConfirmed = "order-confirmed";

    /// <summary>Values: <c>greeting</c>, <c>orderNumber</c>, <c>total</c>, <c>shipment</c>.</summary>
    public const string OrderShipped = "order-shipped";

    /// <summary>Values: <c>greeting</c>, <c>orderNumber</c>, <c>total</c>, <c>shipment</c>, <c>trackingUrl</c>.</summary>
    public const string OrderShippedWithTracking = "order-shipped-tracking";

    /// <summary>Values: <c>greeting</c>, <c>orderNumber</c>, <c>total</c>, <c>reason</c>.</summary>
    public const string OrderCancelled = "order-cancelled";

    /// <summary>Values: <c>greeting</c>, <c>orderNumber</c>, <c>total</c>, <c>reason</c>.</summary>
    public const string OrderPaymentFailed = "order-payment-failed";
}
