using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkInventory : NetworkBehaviour
{
    [Serializable]
    public struct ItemConfig
    {
        public int itemId;
        public string displayName;
        public int maxStack;
    }

    [Serializable]
    public struct InventoryEntry
    {
        public int itemId;
        public int amount;

        public InventoryEntry(int itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }
    }

    public class SyncInventory : SyncList<InventoryEntry>
    {
    }

    [Header("Inventory")]
    [SerializeField] private int maxSlots = 24;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private string itemDatabaseResourcePath = "ItemDatabase";

    [Header("Legacy Fallback")]
    [SerializeField] private List<ItemConfig> itemConfigs = new();

    [Header("Debug Test (No Backend)")]
    [SerializeField] private bool enableDebugTesting = true;
    [SerializeField] private bool grantStarterItemOnServerSpawn = true;
    [SerializeField] private int debugItemId = 1;
    [SerializeField] private int debugAmount = 1;
    [SerializeField] private Key debugAddKey = Key.F6;
    [SerializeField] private Key debugUseKey = Key.F7;
    [SerializeField] private Key debugRemoveKey = Key.F8;
    [SerializeField] private Key debugPrintKey = Key.F9;

    public event Action OnInventoryChanged;

    private readonly SyncInventory items = new();
    private readonly List<InventoryEntry> snapshot = new();
    private readonly Dictionary<int, ItemConfig> configById = new();

    public IReadOnlyList<InventoryEntry> Items => snapshot;
    public int MaxSlots => maxSlots;

    private void Awake()
    {
        BuildConfigCache();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        items.OnChange += OnItemsChanged;
        RebuildSnapshot();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!enableDebugTesting || !grantStarterItemOnServerSpawn) return;
        if (debugItemId <= 0 || debugAmount <= 0) return;

        TryAddItemServer(debugItemId, debugAmount);
    }

    public override void OnStopClient()
    {
        items.OnChange -= OnItemsChanged;
        base.OnStopClient();
    }

    private void Update()
    {
        if (!enableDebugTesting) return;
        if (!isLocalPlayer) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[debugAddKey].wasPressedThisFrame)
        {
            CmdTryAddItem(debugItemId, Mathf.Max(1, debugAmount));
        }

        if (Keyboard.current[debugUseKey].wasPressedThisFrame)
        {
            CmdTryUseItem(debugItemId, Mathf.Max(1, debugAmount));
        }

        if (Keyboard.current[debugRemoveKey].wasPressedThisFrame)
        {
            CmdTryRemoveItem(debugItemId, Mathf.Max(1, debugAmount));
        }

        if (Keyboard.current[debugPrintKey].wasPressedThisFrame)
        {
            PrintLocalSnapshot();
        }
    }

    [Command]
    public void CmdTryAddItem(int itemId, int amount)
    {
        if (amount <= 0) return;
        TryAddItemServer(itemId, amount);
    }

    [Command]
    public void CmdTryRemoveItem(int itemId, int amount)
    {
        if (amount <= 0) return;
        TryRemoveItemServer(itemId, amount);
    }

    [Command]
    public void CmdTryUseItem(int itemId, int amount)
    {
        if (amount <= 0) return;
        if (!TryRemoveItemServer(itemId, amount)) return;

        RpcOnItemUsed(itemId, amount);
    }

    [Server]
    public bool TryAddItemServer(int itemId, int amount)
    {
        if (!configById.TryGetValue(itemId, out ItemConfig cfg))
        {
            return false;
        }

        int maxStack = Mathf.Max(1, cfg.maxStack);
        int remaining = amount;

        for (int i = 0; i < items.Count && remaining > 0; i++)
        {
            InventoryEntry entry = items[i];
            if (entry.itemId != itemId || entry.amount >= maxStack)
            {
                continue;
            }

            int room = maxStack - entry.amount;
            int add = Mathf.Min(room, remaining);
            entry.amount += add;
            remaining -= add;
            items[i] = entry;
        }

        while (remaining > 0 && items.Count < Mathf.Max(1, maxSlots))
        {
            int add = Mathf.Min(maxStack, remaining);
            items.Add(new InventoryEntry(itemId, add));
            remaining -= add;
        }

        return remaining == 0;
    }

    [Server]
    public bool TryRemoveItemServer(int itemId, int amount)
    {
        int total = GetTotalAmountServer(itemId);
        if (total < amount)
        {
            return false;
        }

        int remaining = amount;
        for (int i = items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventoryEntry entry = items[i];
            if (entry.itemId != itemId)
            {
                continue;
            }

            if (entry.amount <= remaining)
            {
                remaining -= entry.amount;
                items.RemoveAt(i);
                continue;
            }

            entry.amount -= remaining;
            remaining = 0;
            items[i] = entry;
        }

        return true;
    }

    [Server]
    public int GetTotalAmountServer(int itemId)
    {
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemId == itemId)
            {
                total += items[i].amount;
            }
        }

        return total;
    }

    public int GetTotalAmountClient(int itemId)
    {
        int total = 0;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i].itemId == itemId)
            {
                total += snapshot[i].amount;
            }
        }

        return total;
    }

    public bool TryGetConfig(int itemId, out ItemConfig config)
    {
        return configById.TryGetValue(itemId, out config);
    }

    [ContextMenu("Inventory/Rebuild Config Cache")]
    public void RebuildConfigCacheFromSource()
    {
        BuildConfigCache();
    }

    [ClientRpc]
    private void RpcOnItemUsed(int itemId, int amount)
    {
        if (!isLocalPlayer)
        {
            return;
        }

        Debug.Log($"[Inventory] Used item {itemId} x{amount}");
    }

    private void OnItemsChanged(SyncInventory.Operation op, int index, InventoryEntry item)
    {
        RebuildSnapshot();
    }

    private void RebuildSnapshot()
    {
        snapshot.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            snapshot.Add(items[i]);
        }

        if (isLocalPlayer)
        {
            OnInventoryChanged?.Invoke();
        }
    }

    private void BuildConfigCache()
    {
        configById.Clear();

        ItemDatabase resolvedDatabase = ResolveItemDatabase();
        if (resolvedDatabase != null)
        {
            IReadOnlyList<ItemDefinition> defs = resolvedDatabase.Items;
            for (int i = 0; i < defs.Count; i++)
            {
                ItemDefinition def = defs[i];
                if (def == null || def.ItemId <= 0) continue;

                configById[def.ItemId] = new ItemConfig
                {
                    itemId = def.ItemId,
                    displayName = string.IsNullOrWhiteSpace(def.DisplayName) ? $"Item {def.ItemId}" : def.DisplayName,
                    maxStack = Mathf.Max(1, def.MaxStack),
                };
            }

            if (configById.Count > 0)
            {
                return;
            }
        }

        for (int i = 0; i < itemConfigs.Count; i++)
        {
            ItemConfig cfg = itemConfigs[i];
            if (cfg.itemId <= 0)
            {
                continue;
            }

            if (cfg.maxStack <= 0)
            {
                cfg.maxStack = 1;
            }

            configById[cfg.itemId] = cfg;
        }

        if (configById.Count == 0)
        {
            Debug.LogWarning("[Inventory] No item config source found. Assign ItemDatabase or fill legacy Item Configs.");
        }
    }

    private ItemDatabase ResolveItemDatabase()
    {
        if (itemDatabase != null)
        {
            return itemDatabase;
        }

        if (string.IsNullOrWhiteSpace(itemDatabaseResourcePath))
        {
            return null;
        }

        itemDatabase = Resources.Load<ItemDatabase>(itemDatabaseResourcePath);
        return itemDatabase;
    }

    [ContextMenu("Debug/Print Local Snapshot")]
    private void PrintLocalSnapshot()
    {
        if (snapshot.Count == 0)
        {
            Debug.Log("[Inventory] Snapshot empty.");
            return;
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            InventoryEntry entry = snapshot[i];
            string name = configById.TryGetValue(entry.itemId, out ItemConfig cfg)
                ? cfg.displayName
                : $"Item {entry.itemId}";

            Debug.Log($"[Inventory] Slot {i}: {name} (id={entry.itemId}) x{entry.amount}");
        }
    }
}
