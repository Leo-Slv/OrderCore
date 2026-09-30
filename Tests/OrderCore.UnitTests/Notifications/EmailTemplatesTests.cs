using FluentAssertions;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>The templates in the repository render in Portuguese, with values encoded where they land in HTML.</summary>
public sealed class EmailTemplatesTests
{
    private static readonly EmbeddedEmailTemplates Templates = new();

    private static Dictionary<string, string> Values(string name = "Jane") => new()
    {
        ["name"] = name,
        ["link"] = "https://shop.example/redefinir-senha?token=abc&x=1",
        ["validFor"] = "30 minutos",
    };

    [Fact]
    public void Every_template_renders_with_its_values()
    {
        Templates.Names.Should().BeEquivalentTo(EmailTemplateNames.PasswordReset, EmailTemplateNames.EmailConfirmation);

        foreach (var name in Templates.Names)
        {
            var email = Templates.Render(name, Values());

            email.Subject.Should().NotBeNullOrWhiteSpace();
            email.HtmlBody.Should().NotContain("{{").And.StartWith("<!DOCTYPE html>").And.Contain("lang=\"pt-BR\"");
            email.TextBody.Should().NotContain("{{").And.Contain("https://shop.example/redefinir-senha?token=abc&x=1");
        }
    }

    [Fact]
    public void The_password_reset_email_is_in_portuguese()
    {
        var email = Templates.Render(EmailTemplateNames.PasswordReset, Values());

        email.Subject.Should().Be("Redefinição de senha");
        email.HtmlBody.Should().Contain("Olá, Jane.").And.Contain("O link vale por 30 minutos");
        email.HtmlBody.Should().Contain("href=\"https://shop.example/redefinir-senha?token=abc&amp;x=1\"");
        email.TextBody.Should().StartWith("Olá, Jane.").And.Contain("Este é um e-mail automático");
    }

    [Fact]
    public void Values_are_html_encoded_in_the_html_part_only()
    {
        var email = Templates.Render(EmailTemplateNames.PasswordReset, Values("<script>alert(1)</script>"));

        email.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
        email.TextBody.Should().Contain("Olá, <script>alert(1)</script>.");
    }

    [Fact]
    public void A_value_that_looks_like_a_placeholder_is_not_filled_again()
    {
        var email = Templates.Render(EmailTemplateNames.PasswordReset, Values("{{content}}"));

        email.HtmlBody.Should().Contain("Olá, {{content}}.");
    }

    [Fact]
    public void A_missing_value_is_an_error_rather_than_a_half_filled_email()
    {
        var values = Values();
        values.Remove("link");

        var render = () => Templates.Render(EmailTemplateNames.PasswordReset, values);

        render.Should().Throw<ArgumentException>().WithMessage("*'link'*");
    }

    [Fact]
    public void An_unknown_template_is_an_error()
    {
        var render = () => Templates.Render("welcome", Values());

        render.Should().Throw<ArgumentException>().WithMessage("*'welcome'*");
    }
}
