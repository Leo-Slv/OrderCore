namespace OrderCore.Api.Shared.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would break a business invariant or an
/// aggregate's state machine (e.g. confirming an order that isn't
/// PendingPayment). <see cref="Code"/> is a stable, snake_case identifier
/// the API returns in its <c>ProblemDetails</c> body so a client can branch
/// on the failure without parsing the message. Lives in the domain kernel
/// so any module's Domain layer can throw it without depending on
/// anything outside <c>Shared/Domain</c>.
/// </summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
