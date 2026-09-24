namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

public sealed class UserAccountPersistenceModel
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastSignedInAt { get; set; }

    public int Version { get; set; }

    public ICollection<RefreshSessionPersistenceModel> Sessions { get; set; } = new List<RefreshSessionPersistenceModel>();
}
