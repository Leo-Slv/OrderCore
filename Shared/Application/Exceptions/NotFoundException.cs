namespace OrderCore.Api.Shared.Application.Exceptions;

/// <summary>
/// Thrown by a use case when the resource it was asked to act on does not
/// exist. Mapped to HTTP 404 by <c>ApiExceptionHandler</c>; see
/// <see cref="Domain.Exceptions.DomainRuleViolationException"/> for what
/// <see cref="Code"/> is for.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
