namespace OrderCore.Api.Modules.Notifications.Infrastructure.Templates;

/// <summary>
/// Section <c>Notifications:Brand</c>: how the store presents itself in
/// e-mail (Docs/specs/notifications/email-branding.md). The defaults are the
/// neutral OrderCore look; a deployment sets its own.
/// </summary>
public sealed class EmailBrandOptions
{
    public const string SectionName = "Notifications:Brand";

    /// <summary>The store's name, used in running text and the plain-text footer.</summary>
    public string Name { get; set; } = "OrderCore";

    /// <summary>What the header shows as the logo; the <see cref="Name"/> when empty.</summary>
    public string? Wordmark { get; set; }

    /// <summary>A mark drawn in the accent color after the wordmark (e.g. <c>.</c>).</summary>
    public string WordmarkAccent { get; set; } = string.Empty;

    /// <summary>Buttons, links and the wordmark mark; <c>#rrggbb</c>.</summary>
    public string AccentColor { get; set; } = "#18181b";

    /// <summary>The soft tint behind the icons; <c>#rrggbb</c>.</summary>
    public string AccentSoftColor { get; set; } = "#f4f4f5";

    /// <summary>The line in the dark footer.</summary>
    public string Footer { get; set; } = "Este é um e-mail automático, por favor não responda.";
}
