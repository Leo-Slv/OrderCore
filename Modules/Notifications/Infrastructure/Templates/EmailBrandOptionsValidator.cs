using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Templates;

/// <summary>
/// Stops the API at startup on a brand the e-mails can't render safely: the
/// colors land in inline CSS, so they must be <c>#rrggbb</c>; the name and the
/// footer can't be empty.
/// </summary>
public sealed partial class EmailBrandOptionsValidator : IValidateOptions<EmailBrandOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailBrandOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            failures.Add("Notifications:Brand:Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Footer))
        {
            failures.Add("Notifications:Brand:Footer is required.");
        }

        if (!HexColor().IsMatch(options.AccentColor ?? string.Empty))
        {
            failures.Add("Notifications:Brand:AccentColor must be a color like #3b3fd9.");
        }

        if (!HexColor().IsMatch(options.AccentSoftColor ?? string.Empty))
        {
            failures.Add("Notifications:Brand:AccentSoftColor must be a color like #ececfd.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
