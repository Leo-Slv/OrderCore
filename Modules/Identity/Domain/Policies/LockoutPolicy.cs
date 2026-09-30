namespace OrderCore.Api.Modules.Identity.Domain.Policies;

/// <summary>
/// How many wrong passwords in a row lock an account, and for how long
/// (production-readiness spec, decision 4: 5 in a row, 15 minutes).
/// </summary>
public sealed record LockoutPolicy(int MaxFailedAttempts, TimeSpan Duration)
{
    public static readonly LockoutPolicy Default = new(5, TimeSpan.FromMinutes(15));
}
