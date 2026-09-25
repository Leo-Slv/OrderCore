using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when a new stock
/// item hits the unique product index because another request created the
/// product's stock record first. An Application-layer type so use cases
/// don't reference the database driver; <c>EnsureStockItemUseCase</c>
/// treats it as "already exists".
/// </summary>
public sealed class DuplicateStockItemException : ConflictException
{
    public const string ErrorCode = "stock_item_already_exists";

    public DuplicateStockItemException(string message, Exception innerException)
        : base(ErrorCode, message, innerException)
    {
    }
}
