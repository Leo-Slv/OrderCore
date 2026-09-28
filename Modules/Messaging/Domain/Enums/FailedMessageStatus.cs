namespace OrderCore.Api.Modules.Messaging.Domain.Enums;

public enum FailedMessageStatus
{
    /// <summary>Exhausted its attempts; waiting for an admin to replay or discard it.</summary>
    Pending,

    /// <summary>Sent back to its consumer's queue by an admin.</summary>
    Replayed,

    /// <summary>Given up on by an admin; never retried again.</summary>
    Discarded,
}
