using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterLootTable", menuName = "TwinBlades/Loot/Monster Loot Table")]
public class MonsterLootTable : ScriptableObject
{
    [Serializable]
    public class StackableDropRule
    {
        public int itemId = 1;
        [Range(0f, 1f)] public float dropChance = 0.5f;
        public int minAmount = 1;
        public int maxAmount = 1;
    }

    [Serializable]
    public class WeaponEffectChance
    {
        public WeaponEffectType effectType = WeaponEffectType.None;
        [Range(0f, 1f)] public float chance = 0.05f;
        [Range(0f, 100f)] public float effectChancePercent = 20f;
    }

    [Serializable]
    public class WeaponDropRule
    {
        public int baseItemId = 1001;
        public string baseName = "Sword";
        [Range(0f, 1f)] public float dropChance = 0.15f;

        public int minBonusAttack = 1;
        public int maxBonusAttack = 10;
        [Range(0f, 1f)] public float jackpotAttackChance = 0.05f;
        public int jackpotAttackValue = 50;

        [Range(0f, 100f)] public float minBonusAttackSpeedPercent = 0f;
        [Range(0f, 100f)] public float maxBonusAttackSpeedPercent = 20f;
        [Range(0f, 1f)] public float jackpotAttackSpeedChance = 0.01f;
        [Range(0f, 100f)] public float jackpotAttackSpeedPercent = 70f;

        public List<WeaponEffectChance> effectChances = new();
    }

    [Header("Drops")]
    public List<StackableDropRule> stackableDrops = new();
    public List<WeaponDropRule> weaponDrops = new();
}
