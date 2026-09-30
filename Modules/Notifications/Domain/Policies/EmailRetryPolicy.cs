namespace OrderCore.Api.Modules.Notifications.Domain.Policies;

/// <summary>
/// How long to wait before each new attempt after a transient failure
/// (Docs/specs/identity/password-recovery-implementation-plan.md, stage 1):
/// one attempt, then one more after each delay — with the default 30 s,
/// 2 min, 10 min and 30 min, five attempts over about 42 minutes.
/// </summary>
public sealed class EmailRetryPolicy
{
    public static readonly EmailRetryPolicy Default = new(
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
    ]);

    public EmailRetryPolicy(IReadOnlyList<TimeSpan> delays)
    {
        if (delays.Any(d => d < TimeSpan.Zero))
        {
            throw new ArgumentException("Retry delays can't be negative.", nameof(delays));
        }

        Delays = delays;
    }

    public IReadOnlyList<TimeSpan> Delays { get; }

    public int MaxAttempts => Delays.Count + 1;

    /// <summary>The wait before the next attempt, or null when <paramref name="attemptsSoFar"/> used them all.</summary>
    public TimeSpan? DelayAfter(int attemptsSoFar) =>
        attemptsSoFar >= 1 && attemptsSoFar <= Delays.Count ? Delays[attemptsSoFar - 1] : null;
}
