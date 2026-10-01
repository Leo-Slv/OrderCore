namespace OrderCore.Api.Modules.Identity.Domain.Enums;

/// <summary>What a single-use <c>AccountToken</c> e-mailed to the account's owner allows.</summary>
public enum AccountTokenPurpose
{
    PasswordReset,
    EmailConfirmation,
}
