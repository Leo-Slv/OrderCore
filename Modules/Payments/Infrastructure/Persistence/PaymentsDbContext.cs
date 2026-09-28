using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// Owns only the Payments module's own tables — see
/// <c>CustomersDbContext</c>'s remarks on why each module gets its own
/// DbContext instead of one project-wide context.
///
/// Also holds the module's outbox (<c>payments_outbox_messages</c>, the
/// shared messaging shape), so a payment and the events it raised are
/// saved in one transaction; the Messaging relay publishes them.
/// </summary>
public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentPersistenceModel> Payments => Set<PaymentPersistenceModel>();

    public DbSet<RefundPersistenceModel> Refunds => Set<RefundPersistenceModel>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Configurations");
        modelBuilder.AddOutbox("payments");
    }
}
