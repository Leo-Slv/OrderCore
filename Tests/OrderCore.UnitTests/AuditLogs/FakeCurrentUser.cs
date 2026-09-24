using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.UnitTests.AuditLogs;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; init; }

    public Guid? CustomerId { get; init; }

    public string? Role { get; init; }
}
