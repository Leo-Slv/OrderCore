using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;

namespace OrderCore.UnitTests.Identity;

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(UserAccount account, DateTimeOffset now) =>
        new($"access:{account.Id}:{account.Role}:{account.CustomerId}", now.AddMinutes(15));
}
