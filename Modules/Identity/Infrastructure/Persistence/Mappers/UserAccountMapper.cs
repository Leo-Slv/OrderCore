using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between <see cref="UserAccount"/> and its persistence models,
/// the same way every other module's mapper does: <c>ToDomain</c> goes
/// through <c>Rehydrate</c>, and <c>ApplyChanges</c> copies <c>Version</c>
/// and reconciles the sessions (added on sign-in/rotation, removed when
/// pruned).
/// </summary>
public static class UserAccountMapper
{
    public static UserAccount ToDomain(UserAccountPersistenceModel model) => UserAccount.Rehydrate(
        model.Id,
        model.Email,
        model.PasswordHash,
        Enum.Parse<UserRole>(model.Role),
        model.CustomerId,
        model.Active,
        model.CreatedAt,
        model.UpdatedAt,
        model.LastSignedInAt,
        model.Version,
        model.Sessions.Select(ToDomain));

    public static UserAccountPersistenceModel ToPersistence(UserAccount domain) => new()
    {
        Id = domain.Id,
        Email = domain.Email,
        NormalizedEmail = domain.NormalizedEmail,
        PasswordHash = domain.PasswordHash,
        Role = domain.Role.ToString(),
        CustomerId = domain.CustomerId,
        Active = domain.Active,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
        LastSignedInAt = domain.LastSignedInAt,
        Version = domain.Version,
        Sessions = domain.Sessions.Select(s => ToPersistence(s, domain.Id)).ToList(),
    };

    public static void ApplyChanges(UserAccount domain, UserAccountPersistenceModel model)
    {
        model.PasswordHash = domain.PasswordHash;
        model.CustomerId = domain.CustomerId;
        model.Active = domain.Active;
        model.UpdatedAt = domain.UpdatedAt;
        model.LastSignedInAt = domain.LastSignedInAt;
        model.Version = domain.Version;

        ChildCollectionReconciler.Reconcile(
            domain.Sessions, model.Sessions, s => ToPersistence(s, domain.Id), ApplyChanges, s => s.Id);
    }

    private static RefreshSessionPersistenceModel ToPersistence(RefreshSession domain, Guid userAccountId) => new()
    {
        Id = domain.Id,
        UserAccountId = userAccountId,
        TokenHash = domain.TokenHash,
        FamilyId = domain.FamilyId,
        CreatedAt = domain.CreatedAt,
        ExpiresAt = domain.ExpiresAt,
        RevokedAt = domain.RevokedAt,
        ReplacedBySessionId = domain.ReplacedBySessionId,
    };

    private static void ApplyChanges(RefreshSession domain, RefreshSessionPersistenceModel model)
    {
        model.RevokedAt = domain.RevokedAt;
        model.ReplacedBySessionId = domain.ReplacedBySessionId;
    }

    private static RefreshSession ToDomain(RefreshSessionPersistenceModel model) => RefreshSession.Rehydrate(
        model.Id, model.TokenHash, model.FamilyId, model.CreatedAt, model.ExpiresAt, model.RevokedAt, model.ReplacedBySessionId);
}
