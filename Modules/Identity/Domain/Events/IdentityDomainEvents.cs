using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Identity.Domain.Events;

public sealed record UserAccountCreated(Guid EventId, DateTimeOffset OccurredAt, Guid UserAccountId, UserRole Role)
    : IDomainEvent;

/// <summary>
/// An already-rotated or revoked refresh token was presented again: a
/// likely stolen token. Every session in the family was revoked.
/// </summary>
public sealed record RefreshTokenReuseDetected(Guid EventId, DateTimeOffset OccurredAt, Guid UserAccountId, Guid FamilyId)
    : IDomainEvent;
