namespace OrderCore.Api.Modules.Notifications.Domain.Enums;

public enum EmailStatus
{
    /// <summary>Waiting to be sent: never tried yet, or tried and due again.</summary>
    Pending,

    Sent,

    /// <summary>Given up on: refused by the provider, or every attempt failed.</summary>
    Failed,
}
