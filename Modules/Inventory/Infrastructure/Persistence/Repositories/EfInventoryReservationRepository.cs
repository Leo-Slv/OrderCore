using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;

/// <summary>
/// No `SaveChangesAsync` of its own — see <c>IUnitOfWork</c>'s remarks.
/// </summary>
public sealed class EfInventoryReservationRepository : IInventoryReservationRepository, IPendingChangesTracker
{
    private readonly InventoryDbContext _dbContext;
    private readonly Dictionary<Guid, (InventoryReservation Domain, InventoryReservationPersistenceModel Model)> _tracked = new();

    public EfInventoryReservationRepository(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var model = await _dbContext.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<InventoryReservation>> ListByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var models = await _dbContext.Reservations.Where(r => r.OrderId == orderId).ToListAsync(cancellationToken);
        return models.Select(Track).ToList();
    }

    public async Task<(IReadOnlyList<InventoryReservation> Items, int TotalCount)> ListByProductIdAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.Reservations.AsNoTracking().Where(r => r.ProductId == productId);

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(r => r.ReservedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(InventoryReservationMapper.ToDomain).ToList(), totalCount);
    }

    public async Task AddAsync(InventoryReservation reservation, CancellationToken cancellationToken)
    {
        var model = InventoryReservationMapper.ToPersistence(reservation);
        await _dbContext.Reservations.AddAsync(model, cancellationToken);
        _tracked[reservation.Id] = (reservation, model);
    }

    private InventoryReservation Track(InventoryReservationPersistenceModel model)
    {
        var domain = InventoryReservationMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    void IPendingChangesTracker.ApplyPendingChanges()
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            InventoryReservationMapper.ApplyChanges(domain, model);
        }
    }

    IReadOnlyCollection<IDomainEvent> IPendingChangesTracker.CollectAndClearDomainEvents()
    {
        var events = _tracked.Values.SelectMany(t => t.Domain.DomainEvents).ToList();

        foreach (var (domain, _) in _tracked.Values)
        {
            domain.ClearDomainEvents();
        }

        return events;
    }

    void IPendingChangesTracker.ForgetTrackedEntries()
    {
        foreach (var (_, model) in _tracked.Values)
        {
            _dbContext.Entry(model).State = EntityState.Detached;
        }

        _tracked.Clear();
    }
}
