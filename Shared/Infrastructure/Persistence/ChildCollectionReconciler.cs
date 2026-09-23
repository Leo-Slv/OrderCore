using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Shared.Infrastructure.Persistence;

/// <summary>
/// Reconciles an EF Core-tracked child collection (e.g.
/// <c>CustomerAddressPersistenceModel</c>, <c>ProductImagePersistenceModel</c>)
/// with the current state of an aggregate's domain child entities: updates
/// models that still exist, removes ones the aggregate no longer has, and
/// adds new ones — instead of clearing and re-adding the whole collection,
/// which would make EF Core delete and re-insert every child on every save.
/// Used by every module's <c>&lt;Entity&gt;Mapper.ApplyChanges</c> — see
/// the Persistence section of claude.md.
/// </summary>
public static class ChildCollectionReconciler
{
    public static void Reconcile<TDomain, TModel>(
        IReadOnlyCollection<TDomain> domainChildren,
        ICollection<TModel> modelChildren,
        Func<TDomain, TModel> toPersistence,
        Action<TDomain, TModel> applyChanges,
        Func<TModel, Guid> modelId)
        where TDomain : Entity<Guid>
    {
        var domainById = domainChildren.ToDictionary(d => d.Id);

        foreach (var model in modelChildren.Where(m => !domainById.ContainsKey(modelId(m))).ToList())
        {
            modelChildren.Remove(model);
        }

        var modelById = modelChildren.ToDictionary(modelId);

        foreach (var domain in domainChildren)
        {
            if (modelById.TryGetValue(domain.Id, out var model))
            {
                applyChanges(domain, model);
            }
            else
            {
                modelChildren.Add(toPersistence(domain));
            }
        }
    }
}
