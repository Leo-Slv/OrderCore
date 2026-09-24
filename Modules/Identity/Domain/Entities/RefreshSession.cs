using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Identity.Domain.Entities;

/// <summary>
/// One refresh token issued to a <see cref="UserAccount"/>. Only a hash of
/// the token is kept, never the token itself. Every session started by a
/// sign-in begins a new <see cref="FamilyId"/>; each rotation replaces the
/// session with a successor in the same family, which is what lets a
/// replayed old token revoke everything descended from that sign-in.
/// </summary>
public sealed class RefreshSession : Entity<Guid>
{
    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedBySessionId { get; private set; }

    private RefreshSession()
    {
    }

    private RefreshSession(Guid id, string tokenHash, Guid familyId, DateTimeOffset createdAt, DateTimeOffset expiresAt) : base(id)
    {
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    internal static RefreshSession Create(string tokenHash, Guid familyId, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("A token hash is required.", nameof(tokenHash));
        }

        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "A session must expire in the future.");
        }

        return new RefreshSession(Guid.NewGuid(), tokenHash, familyId, now, expiresAt);
    }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && !IsExpired(now);

    internal void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    internal void ReplaceWith(RefreshSession successor, DateTimeOffset now)
    {
        ReplacedBySessionId = successor.Id;
        Revoke(now);
    }

    internal static RefreshSession Rehydrate(
        Guid id,
        string tokenHash,
        Guid familyId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedBySessionId) =>
        new(id, tokenHash, familyId, createdAt, expiresAt)
        {
            RevokedAt = revokedAt,
            ReplacedBySessionId = replacedBySessionId,
        };
}
