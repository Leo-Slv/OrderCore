using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary>
/// Thrown by <see cref="IUserAccountRepository.SaveChangesAsync"/> when a
/// concurrent request changed the account since it was loaded — the same
/// shape as Inventory's <c>StockConcurrencyConflictException</c>, so use
/// cases can react to it without referencing EF Core. Unhandled, it reaches
/// the client as <c>409 concurrency_conflict</c>, as before.
/// </summary>
public sealed class AccountConcurrencyConflictException : ConflictException
{
    public const string ErrorCode = "concurrency_conflict";

    public AccountConcurrencyConflictException(string message, Exception innerException)
        : base(ErrorCode, message, innerException)
    {
    }
}
