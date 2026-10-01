namespace OrderCore.Api.Shared.Application.Exceptions;

/// <summary>
/// Thrown by a use case when the caller is signed in and the resource is
/// theirs, but something about their account keeps them from doing this yet
/// (e.g. <c>email_not_confirmed</c> at checkout). Mapped to HTTP 403 by
/// <c>ApiExceptionHandler</c>, with the code telling the client what to do
/// about it. Not for "this isn't yours" — that is a 404 — nor for the
/// authorization policies, which answer 403 on their own.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
