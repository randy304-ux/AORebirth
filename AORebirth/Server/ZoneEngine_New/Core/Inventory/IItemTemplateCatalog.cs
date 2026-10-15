namespace ZoneEngine_New.Core.Inventory
{
    using System.Collections.Generic;

    public interface IItemTemplateCatalog
    {
        bool TryGet(int aoid, out ItemTemplate template);

        ItemTemplate Require(int aoid);

        /// <summary>Every template loaded from items.dat. Empty when the catalog failed to load.</summary>
        IReadOnlyCollection<ItemTemplate> All { get; }
    }
}
