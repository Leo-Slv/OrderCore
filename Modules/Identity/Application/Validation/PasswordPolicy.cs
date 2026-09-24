using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.Validation;

/// <summary>
/// Minimum password rules: 8 to 128 characters, at least one letter and
/// one digit. The upper bound keeps hashing cost bounded. A weak password
/// is reported as <c>weak_password</c> (400) so the sign-up form can
/// point at the password field.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    public static void EnsureAcceptable(string password)
    {
        if (string.IsNullOrEmpty(password)
            || password.Length is < MinimumLength or > MaximumLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new DomainRuleViolationException(
                "weak_password",
                $"The password must have {MinimumLength} to {MaximumLength} characters, including at least one letter and one digit.");
        }
    }
}
