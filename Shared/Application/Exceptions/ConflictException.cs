namespace OrderCore.Api.Shared.Application.Exceptions;

/// <summary>
/// Thrown by a use case when the request is valid on its own but conflicts
/// with the current state of the system (a duplicate SKU/email, not enough
/// stock to reserve). Mapped to HTTP 409 by <c>ApiExceptionHandler</c>; see
/// <see cref="Domain.Exceptions.DomainRuleViolationException"/> for what
/// <see cref="Code"/> is for. Not sealed: a module can derive a more
/// specific conflict (e.g. Inventory's <c>StockConcurrencyConflictException</c>)
/// that its own use cases catch by type, while it still reaches the client
/// as a 409 without <c>Shared</c> knowing about that module.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public ConflictException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
