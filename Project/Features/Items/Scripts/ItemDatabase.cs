using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "TwinBlades/Items/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();

    private Dictionary<int, ItemDefinition> byId;

    public bool TryGetItem(int itemId, out ItemDefinition item)
    {
        EnsureCache();
        return byId.TryGetValue(itemId, out item);
    }

    public IReadOnlyList<ItemDefinition> Items
    {
        get
        {
            EnsureCache();
            return items;
        }
    }

    public void RebuildCache()
    {
        byId = new Dictionary<int, ItemDefinition>();

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (def == null) continue;
            if (def.ItemId <= 0) continue;

            byId[def.ItemId] = def;
        }
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void EnsureCache()
    {
        if (byId == null)
        {
            RebuildCache();
        }
    }
}
