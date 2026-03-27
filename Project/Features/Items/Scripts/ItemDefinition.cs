using UnityEngine;

public enum ItemKind
{
    Currency = 0,
    Consumable = 1,
    Material = 2,
    Equipment = 3,
    Quest = 4,
}

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "TwinBlades/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private int itemId = 1;
    [SerializeField] private string displayName = "New Item";
    [SerializeField] private ItemKind kind = ItemKind.Material;

    [Header("Stack")]
    [SerializeField] private int maxStack = 99;

    [Header("Visual")]
    [SerializeField] private Sprite icon;
    [SerializeField] private RuntimeAnimatorController lootAnimatorController;

    public int ItemId => itemId;
    public string DisplayName => displayName;
    public ItemKind Kind => kind;
    public int MaxStack => Mathf.Max(1, maxStack);
    public Sprite Icon => icon;
    public RuntimeAnimatorController LootAnimatorController => lootAnimatorController;
}
