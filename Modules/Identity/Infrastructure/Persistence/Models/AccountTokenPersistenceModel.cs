namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

public sealed class AccountTokenPersistenceModel
{
    public Guid Id { get; set; }

    public Guid UserAccountId { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
