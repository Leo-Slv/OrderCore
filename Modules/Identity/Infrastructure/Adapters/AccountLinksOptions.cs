using Microsoft.Extensions.Options;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Adapters;

/// <summary>
/// Section <c>Identity:Links</c>: the storefront pages the account e-mails
/// link to (password-recovery spec, decision 2), each with a <c>{token}</c>
/// placeholder the single-use token replaces — e.g.
/// <c>https://shop.example/redefinir-senha?token={token}</c>.
/// </summary>
public sealed class AccountLinksOptions
{
    public const string SectionName = "Identity:Links";
    public const string TokenPlaceholder = "{token}";

    public string? ResetPassword { get; set; }

    public string? ConfirmEmail { get; set; }

    public string ResetPasswordLink(string token) => Fill(ResetPassword!, token);

    public string ConfirmEmailLink(string token) => Fill(ConfirmEmail!, token);

    private static string Fill(string template, string token) =>
        template.Replace(TokenPlaceholder, Uri.EscapeDataString(token), StringComparison.Ordinal);
}

/// <summary>
/// Stops the API at startup when an account e-mail would carry a broken link:
/// each link must be an absolute http(s) address with a <c>{token}</c>
/// placeholder.
/// </summary>
public sealed class AccountLinksOptionsValidator : IValidateOptions<AccountLinksOptions>
{
    public ValidateOptionsResult Validate(string? name, AccountLinksOptions options)
    {
        var failures = new List<string>();
        Check("ResetPassword", options.ResetPassword, failures);
        Check("ConfirmEmail", options.ConfirmEmail, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(string setting, string? link, List<string> failures)
    {
        var valid = !string.IsNullOrWhiteSpace(link)
            && link.Contains(AccountLinksOptions.TokenPlaceholder, StringComparison.Ordinal)
            && Uri.TryCreate(link.Replace(AccountLinksOptions.TokenPlaceholder, "t", StringComparison.Ordinal), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        if (!valid)
        {
            failures.Add(
                $"{AccountLinksOptions.SectionName}:{setting} must be the storefront page's address with a {{token}} placeholder " +
                "(e.g. https://shop.example/redefinir-senha?token={token}).");
        }
    }
}
