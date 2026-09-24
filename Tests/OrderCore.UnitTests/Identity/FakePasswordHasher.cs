using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.UnitTests.Identity;

/// <summary>Readable, reversible "hash" so tests can see what was stored.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hashed:";

    /// <summary>Hashes starting with this verify but report that they need rehashing.</summary>
    public const string OutdatedPrefix = "old-hashed:";

    public string Hash(string password) => Prefix + password;

    public PasswordCheck Verify(string passwordHash, string password) =>
        passwordHash == Prefix + password ? PasswordCheck.Succeeded
        : passwordHash == OutdatedPrefix + password ? PasswordCheck.SucceededRehashNeeded
        : PasswordCheck.Failed;
}
