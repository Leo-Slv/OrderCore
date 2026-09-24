namespace OrderCore.Api.Shared.Application.Abstractions;

/// <summary>
/// Who is making the current request, as far as the Application layer
/// needs to know: use cases and services ask this instead of reading HTTP
/// claims themselves. Implemented by <c>HttpContextCurrentUser</c>
/// (Shared/Presentation). Outside of an HTTP request (e.g. the outbox
/// background service) nobody is signed in and every property is null.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user account's id (Identity module).</summary>
    Guid? UserId { get; }

    /// <summary>The customer the account belongs to; null for admins and anonymous callers.</summary>
    Guid? CustomerId { get; }

    /// <summary>One of <see cref="UserRoles"/>, or null when nobody is signed in.</summary>
    string? Role { get; }

    bool IsAuthenticated => UserId is not null;

    bool IsAdmin => Role == UserRoles.Admin;
}
