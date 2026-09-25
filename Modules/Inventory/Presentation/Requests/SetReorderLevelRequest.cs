namespace OrderCore.Api.Modules.Inventory.Presentation.Requests;

public sealed class SetReorderLevelRequest
{
    /// <summary>At or below this many available units the item is low stock; 0 turns the alert off.</summary>
    public int ReorderLevel { get; init; }
}
