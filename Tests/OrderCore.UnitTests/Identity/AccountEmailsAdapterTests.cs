using FluentAssertions;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Infrastructure.Adapters;
using Xunit;

namespace OrderCore.UnitTests.Identity;

/// <summary>The links and words the account e-mails are built with.</summary>
public sealed class AccountEmailsAdapterTests
{
    [Theory]
    [InlineData(30, "30 minutos")]
    [InlineData(1, "1 minuto")]
    [InlineData(60, "1 hora")]
    [InlineData(24 * 60, "24 horas")]
    [InlineData(90, "90 minutos")]
    public void Durations_are_written_in_portuguese(int minutes, string expected)
    {
        AccountEmailsAdapter.Duration(TimeSpan.FromMinutes(minutes)).Should().Be(expected);
    }

    [Theory]
    [InlineData("Jane Doe", "Olá, Jane Doe.")]
    [InlineData(null, "Olá!")]
    [InlineData(" ", "Olá!")]
    public void An_admin_without_a_name_is_still_greeted(string? name, string expected)
    {
        AccountEmailsAdapter.Greeting(name).Should().Be(expected);
    }

    [Fact]
    public void The_token_goes_into_the_link_escaped()
    {
        var links = new AccountLinksOptions { ResetPassword = "https://shop.example/redefinir-senha?token={token}" };

        links.ResetPasswordLink("a+b/c=").Should().Be("https://shop.example/redefinir-senha?token=a%2Bb%2Fc%3D");
    }

    [Theory]
    [InlineData("https://shop.example/redefinir-senha?token={token}", true)]
    [InlineData("http://localhost:3000/redefinir-senha?token={token}", true)]
    [InlineData("https://shop.example/redefinir-senha", false)]
    [InlineData("/redefinir-senha?token={token}", false)]
    [InlineData("ftp://shop.example/?token={token}", false)]
    [InlineData("", false)]
    public void Each_link_must_be_a_storefront_address_with_a_token_placeholder(string link, bool valid)
    {
        var options = new AccountLinksOptions { ResetPassword = link, ConfirmEmail = "https://shop.example/confirmar-email?token={token}" };

        var result = new AccountLinksOptionsValidator().Validate(null, options);

        result.Succeeded.Should().Be(valid);
        if (!valid)
        {
            result.Failures.Should().ContainSingle().Which.Should().StartWith("Identity:Links:ResetPassword");
        }
    }
}
