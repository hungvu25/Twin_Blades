using System;

public enum LootDropKind : byte
{
    Stackable = 0,
    Weapon = 1,
}

public enum WeaponEffectType : int
{
    None = 0,
    SlowOnHit = 1,
    BurnOnHit = 2,
    LifeSteal = 3,
}

[Serializable]
public struct WeaponInstanceData
{
    public string instanceId;
    public int baseItemId;
    public string baseName;
    public int bonusAttack;
    public float bonusAttackSpeedPercent;
    public WeaponEffectType effectType;
    public float effectChancePercent;

    public static WeaponInstanceData Empty => new WeaponInstanceData
    {
        instanceId = string.Empty,
        baseItemId = 0,
        baseName = string.Empty,
        bonusAttack = 0,
        bonusAttackSpeedPercent = 0f,
        effectType = WeaponEffectType.None,
        effectChancePercent = 0f,
    };
}

[Serializable]
public struct GeneratedLootDrop
{
    public LootDropKind kind;
    public int itemId;
    public int amount;
    public WeaponInstanceData weapon;

    public static GeneratedLootDrop Stackable(int itemId, int amount)
    {
        return new GeneratedLootDrop
        {
            kind = LootDropKind.Stackable,
            itemId = itemId,
            amount = amount,
            weapon = WeaponInstanceData.Empty,
        };
    }

    public static GeneratedLootDrop Weapon(WeaponInstanceData weapon)
    {
        return new GeneratedLootDrop
        {
            kind = LootDropKind.Weapon,
            itemId = 0,
            amount = 0,
            weapon = weapon,
        };
    }
}
