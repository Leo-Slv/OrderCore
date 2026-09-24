namespace OrderCore.Api.Shared.Application.Exceptions;

/// <summary>
/// Thrown by a use case when the caller could not be authenticated with
/// what they presented (wrong credentials, an unknown or revoked refresh
/// token). Mapped to HTTP 401 by <c>ApiExceptionHandler</c>. Not for "signed
/// in but not allowed": that is a 403 from the authorization policies, or
/// a 404 when a customer asks for something that isn't theirs.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
