namespace OrderCore.Api.Modules.Notifications.Application.Contracts;

/// <summary>The templates Notifications knows (<c>Infrastructure/Templates/&lt;name&gt;.html|.txt</c>).</summary>
public static class EmailTemplateNames
{
    /// <summary>Values: <c>greeting</c>, <c>link</c>, <c>validFor</c>.</summary>
    public const string PasswordReset = "password-reset";

    /// <summary>Values: <c>greeting</c>, <c>link</c>, <c>validFor</c>.</summary>
    public const string EmailConfirmation = "email-confirmation";
}
