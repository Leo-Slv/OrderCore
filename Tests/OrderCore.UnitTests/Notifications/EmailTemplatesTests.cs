using FluentAssertions;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>The templates in the repository render in Portuguese, with values encoded where they land in HTML.</summary>
public sealed class EmailTemplatesTests
{
    private static readonly EmbeddedEmailTemplates Templates = new();

    private static Dictionary<string, string> Values(string greeting = "Olá, Jane.") => new()
    {
        ["greeting"] = greeting,
        ["link"] = "https://shop.example/redefinir-senha?token=abc&x=1",
        ["validFor"] = "30 minutos",
    };

    /// <summary>Every value any template takes.</summary>
    private static Dictionary<string, string> AllValues() => new(Values())
    {
        ["orderNumber"] = "ORD-000042",
        ["total"] = "R$ 99,90",
        ["shipment"] = "Transportadora: Correios.",
        ["trackingUrl"] = "https://track.example/AB1",
        ["reason"] = "O pagamento não foi aprovado pela operadora.",
    };

    [Fact]
    public void Every_template_renders_with_its_values()
    {
        Templates.Names.Should().BeEquivalentTo(
            EmailTemplateNames.PasswordReset,
            EmailTemplateNames.EmailConfirmation,
            EmailTemplateNames.OrderConfirmed,
            EmailTemplateNames.OrderShipped,
            EmailTemplateNames.OrderShippedWithTracking,
            EmailTemplateNames.OrderCancelled,
            EmailTemplateNames.OrderPaymentFailed);

        foreach (var name in Templates.Names)
        {
            var email = Templates.Render(name, AllValues());

            email.Subject.Should().NotBeNullOrWhiteSpace().And.NotContain("{{");
            email.HtmlBody.Should().NotContain("{{").And.StartWith("<!DOCTYPE html>").And.Contain("lang=\"pt-BR\"").And.Contain("Olá, Jane.");
            email.TextBody.Should().NotContain("{{").And.Contain("Olá, Jane.");
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
        email.TextBody.Should().Contain("<script>alert(1)</script>");
    }

    [Fact]
    public void A_value_that_looks_like_a_placeholder_is_not_filled_again()
    {
        var email = Templates.Render(EmailTemplateNames.PasswordReset, Values("{{content}}"));

        email.HtmlBody.Should().Contain(">{{content}}</p>");
    }

    private static readonly EmailBrandOptions Marfim = new()
    {
        Name = "Marfim",
        Wordmark = "marfim",
        WordmarkAccent = ".",
        AccentColor = "#3b3fd9",
        AccentSoftColor = "#ececfd",
        Footer = "Mensagem automática sobre a sua conta ou pedido.",
    };

    [Fact]
    public void The_default_brand_is_the_neutral_ordercore_look()
    {
        var email = Templates.Render(EmailTemplateNames.PasswordReset, Values());

        email.HtmlBody.Should().Contain(">OrderCore<span").And.Contain("#18181b").And.NotContain("#3b3fd9");
        email.TextBody.Should().EndWith("--\nOrderCore\nEste é um e-mail automático, por favor não responda.\n");
    }

    [Fact]
    public void The_configured_brand_shows_in_the_header_buttons_icon_and_footer_of_every_template()
    {
        var branded = new EmbeddedEmailTemplates(Marfim);

        foreach (var name in branded.Names)
        {
            var email = branded.Render(name, AllValues());

            email.HtmlBody.Should().Contain(">marfim<span style=\"color:#3b3fd9;\">.</span>")
                .And.Contain("Mensagem automática sobre a sua conta ou pedido.")
                .And.NotContain("OrderCore");
            email.TextBody.Should().Contain("Marfim\nMensagem automática").And.NotContain("OrderCore");
        }

        branded.Render(EmailTemplateNames.EmailConfirmation, Values()).HtmlBody
            .Should().Contain("background:#3b3fd9;color:#ffffff").And.Contain("background:#ececfd");
    }

    [Fact]
    public void The_wordmark_falls_back_to_the_name()
    {
        var email = new EmbeddedEmailTemplates(new EmailBrandOptions { Name = "Casa Loja" })
            .Render(EmailTemplateNames.PasswordReset, Values());

        email.HtmlBody.Should().Contain(">Casa Loja<span");
    }

    [Fact]
    public void The_tag_and_the_preheader_come_from_the_template_and_are_encoded()
    {
        var email = Templates.Render(EmailTemplateNames.OrderConfirmed, new Dictionary<string, string>(AllValues()) { ["orderNumber"] = "A&B" });

        email.HtmlBody.Should().Contain(">PEDIDO</td>").And.Contain("Pagamento aprovado.");
        email.Subject.Should().Be("Pedido A&B confirmado");
        email.HtmlBody.Should().Contain("<title>Pedido A&amp;B confirmado</title>");
        email.TextBody.Should().NotContain("Tag:").And.NotContain("Preheader:");
    }

    [Fact]
    public void The_layout_is_responsive_and_loads_nothing_from_the_internet()
    {
        var email = Templates.Render(EmailTemplateNames.EmailConfirmation, Values());

        email.HtmlBody.Should().Contain("name=\"viewport\"").And.Contain("@media only screen and (max-width: 480px)");
        email.HtmlBody.Should().NotContain("<link").And.NotContain("<script").And.NotContain("src=\"http").And.NotContain("@import");
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
