using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Identity.Domain.Entities;

/// <summary>
/// A single-use secret sent by e-mail (password-recovery spec, items 2 and 5):
/// a password reset or an e-mail confirmation. Like a refresh session, only a
/// hash of the token is kept; the token itself lives in the e-mail only until
/// it is sent (decision 7).
/// </summary>
public sealed class AccountToken : Entity<Guid>
{
    public AccountTokenPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    private AccountToken()
    {
    }

    private AccountToken(Guid id, AccountTokenPurpose purpose, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt) : base(id)
    {
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    internal static AccountToken Create(AccountTokenPurpose purpose, string tokenHash, TimeSpan lifetime, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("A token hash is required.", nameof(tokenHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "A token must last some time.");
        }

        return new AccountToken(Guid.NewGuid(), purpose, tokenHash, now, now + lifetime);
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    /// <summary>Unused and unexpired.</summary>
    public bool IsUsable(DateTimeOffset now) => UsedAt is null && !IsExpired(now);

    internal void MarkUsed(DateTimeOffset now) => UsedAt ??= now;

    internal static AccountToken Rehydrate(
        Guid id,
        AccountTokenPurpose purpose,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? usedAt) =>
        new(id, purpose, tokenHash, createdAt, expiresAt) { UsedAt = usedAt };
}
