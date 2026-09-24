using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Payments.Infrastructure.Outbox;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// Owns only the Payments module's own tables — see
/// <c>CustomersDbContext</c>'s remarks on why each module gets its own
/// DbContext instead of one project-wide context.
///
/// Exposes <see cref="OutboxMessages"/> instead of 06-payments.md's
/// <c>PaymentEventPersistenceModel</c>/<c>PaymentEvents</c>: nothing in
/// the diagram ever writes a <c>PaymentEventPersistenceModel</c> (no
/// mapper, no repository, no use case creates one), while
/// <c>OutboxWriter</c>/<c>OutboxPublisherBackgroundService</c> both need a
/// concrete table with a <c>ProcessedAt</c> column to actually implement
/// the outbox pattern — treated as a diagram inconsistency (most likely an
/// earlier name for the same concept) rather than two separate tables.
/// </summary>
public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentPersistenceModel> Payments => Set<PaymentPersistenceModel>();

    public DbSet<RefundPersistenceModel> Refunds => Set<RefundPersistenceModel>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Configurations");
    }
}
