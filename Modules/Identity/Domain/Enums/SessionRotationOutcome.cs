namespace OrderCore.Api.Modules.Identity.Domain.Enums;

/// <summary>
/// What <c>UserAccount.RotateSession</c> did with a presented refresh
/// token. Only <see cref="Rotated"/> means a new session was issued; the
/// caller treats every other outcome as "invalid refresh token", without
/// telling the client which one it was.
/// </summary>
public enum SessionRotationOutcome
{
    Rotated,

    /// <summary>The token isn't one of this account's sessions.</summary>
    Unknown,

    /// <summary>The session expired; nothing was changed.</summary>
    Expired,

    /// <summary>
    /// The session was already rotated or revoked, so someone is replaying
    /// an old token. The whole session family has been revoked.
    /// </summary>
    Reused,

    /// <summary>The account is deactivated.</summary>
    AccountInactive,
}
