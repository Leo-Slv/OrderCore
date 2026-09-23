namespace OrderCore.Api.Shared.Domain;

/// <summary>
/// Base type for aggregate roots. An aggregate root is the only entry point
/// through which changes to the aggregate's internal state are allowed
/// (see Docs/adr and section 8 of the project context: no anemic entities,
/// no state mutated from outside via public setters).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Optimistic concurrency token. Incremented on every state-changing
    /// operation and mapped to a rowversion / xmin-style column so that
    /// concurrent writes to the same aggregate are detected instead of
    /// silently overwriting each other (see section 11 of the project
    /// context and ADR-009).
    /// </summary>
    public int Version { get; protected set; }

    protected void IncrementVersion() => Version++;
}
