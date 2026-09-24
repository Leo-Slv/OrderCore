using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Domain.Events;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Identity.Domain.Entities;

/// <summary>
/// Someone who can sign in: a customer (linked to a <c>Customer</c> in the
/// Customers module by <see cref="CustomerId"/>) or an admin (no customer).
/// Holds the credentials the Customers module deliberately doesn't. The
/// refresh sessions are children of this aggregate, so rotating a session
/// (revoke it and add its successor) and reacting to a replayed token
/// (revoke the whole family) are each a single-aggregate change saved at
/// once, and the module needs no unit of work.
/// </summary>
public sealed class UserAccount : AggregateRoot<Guid>
{
    private readonly List<RefreshSession> _sessions = new();

    public string Email { get; private set; } = string.Empty;

    /// <summary>What e-mail lookups and the uniqueness index use: trimmed and upper-cased.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public Guid? CustomerId { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public IReadOnlyCollection<RefreshSession> Sessions => _sessions.AsReadOnly();

    private UserAccount()
    {
    }

    private UserAccount(Guid id, string email, string passwordHash, UserRole role, DateTimeOffset now) : base(id)
    {
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        PasswordHash = passwordHash;
        Role = role;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    /// <summary>
    /// A customer account starts without a <see cref="CustomerId"/>: sign-up
    /// reserves the e-mail here first, then creates the customer, then links
    /// it with <see cref="LinkCustomer"/>.
    /// </summary>
    public static UserAccount CreateCustomer(string email, string passwordHash, DateTimeOffset now) =>
        Create(email, passwordHash, UserRole.Customer, now);

    public static UserAccount CreateAdmin(string email, string passwordHash, DateTimeOffset now) =>
        Create(email, passwordHash, UserRole.Admin, now);

    private static UserAccount Create(string email, string passwordHash, UserRole role, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("A valid e-mail is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        var account = new UserAccount(Guid.NewGuid(), email, passwordHash, role, now);
        account.IncrementVersion();
        account.Raise(new UserAccountCreated(Guid.NewGuid(), now, account.Id, role));
        return account;
    }

    public void LinkCustomer(Guid customerId)
    {
        if (Role != UserRole.Customer)
        {
            throw new DomainRuleViolationException("not_a_customer_account", "Only a customer account can be linked to a customer.");
        }

        if (CustomerId is not null)
        {
            throw new DomainRuleViolationException("customer_already_linked", "This account is already linked to a customer.");
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer id is required.", nameof(customerId));
        }

        CustomerId = customerId;
        IncrementVersion();
    }

    /// <summary>Used when the password hasher reports the stored hash is outdated.</summary>
    public void ReplacePasswordHash(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
        UpdatedAt = now;
        IncrementVersion();
    }

    /// <summary>
    /// A successful sign-in: starts a new session family. Expired sessions
    /// are pruned here and on every rotation, so the collection only ever
    /// holds sessions that could still be presented. Revoked but unexpired
    /// ones are kept on purpose: they are how a replay is recognized.
    /// </summary>
    public RefreshSession StartSession(string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        EnsureActive();

        PruneExpiredSessions(now);
        var session = RefreshSession.Create(tokenHash, familyId: Guid.NewGuid(), expiresAt, now);
        _sessions.Add(session);
        LastSignedInAt = now;
        UpdatedAt = now;
        IncrementVersion();
        return session;
    }

    public (SessionRotationOutcome Outcome, RefreshSession? NewSession) RotateSession(
        string presentedTokenHash, string newTokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var current = _sessions.FirstOrDefault(s => s.TokenHash == presentedTokenHash);
        if (current is null)
        {
            return (SessionRotationOutcome.Unknown, null);
        }

        // Before the reuse check: deactivating revokes every session, and a
        // refresh after that is a closed account, not a stolen token.
        if (!Active)
        {
            return (SessionRotationOutcome.AccountInactive, null);
        }

        if (current.RevokedAt is not null)
        {
            RevokeFamily(current.FamilyId, now);
            Raise(new RefreshTokenReuseDetected(Guid.NewGuid(), now, Id, current.FamilyId));
            return (SessionRotationOutcome.Reused, null);
        }

        if (current.IsExpired(now))
        {
            return (SessionRotationOutcome.Expired, null);
        }

        PruneExpiredSessions(now);
        var successor = RefreshSession.Create(newTokenHash, current.FamilyId, expiresAt, now);
        current.ReplaceWith(successor, now);
        _sessions.Add(successor);
        UpdatedAt = now;
        IncrementVersion();
        return (SessionRotationOutcome.Rotated, successor);
    }

    /// <summary>
    /// Sign-out: revokes the whole family the token belongs to (only its
    /// latest session could still be used, but revoking the family is what
    /// "this sign-in is over" means). An unknown token changes nothing.
    /// </summary>
    public void EndSession(string tokenHash, DateTimeOffset now)
    {
        var session = _sessions.FirstOrDefault(s => s.TokenHash == tokenHash);
        if (session is null)
        {
            return;
        }

        RevokeFamily(session.FamilyId, now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        foreach (var session in _sessions)
        {
            session.Revoke(now);
        }

        UpdatedAt = now;
        IncrementVersion();
    }

    private void RevokeFamily(Guid familyId, DateTimeOffset now)
    {
        foreach (var session in _sessions.Where(s => s.FamilyId == familyId))
        {
            session.Revoke(now);
        }

        UpdatedAt = now;
        IncrementVersion();
    }

    private void PruneExpiredSessions(DateTimeOffset now) => _sessions.RemoveAll(s => s.IsExpired(now));

    private void EnsureActive()
    {
        if (!Active)
        {
            throw new DomainRuleViolationException("account_inactive", "This account is deactivated.");
        }
    }

    internal static UserAccount Rehydrate(
        Guid id,
        string email,
        string passwordHash,
        UserRole role,
        Guid? customerId,
        bool active,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? lastSignedInAt,
        int version,
        IEnumerable<RefreshSession> sessions)
    {
        var account = new UserAccount(id, email, passwordHash, role, createdAt)
        {
            CustomerId = customerId,
            Active = active,
            UpdatedAt = updatedAt,
            LastSignedInAt = lastSignedInAt,
            Version = version,
        };

        account._sessions.AddRange(sessions);

        return account;
    }
}
