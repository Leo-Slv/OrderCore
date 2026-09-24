namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

public sealed class RefreshSessionPersistenceModel
{
    public Guid Id { get; set; }

    public Guid UserAccountId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedBySessionId { get; set; }
}
