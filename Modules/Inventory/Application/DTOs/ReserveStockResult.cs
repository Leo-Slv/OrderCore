namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

public sealed record ReserveStockResult(Guid? ReservationId, bool Succeeded);
