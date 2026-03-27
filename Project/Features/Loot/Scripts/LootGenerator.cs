using System;
using System.Collections.Generic;
using UnityEngine;

public static class LootGenerator
{
    public static List<GeneratedLootDrop> RollDrops(MonsterLootTable table)
    {
        List<GeneratedLootDrop> results = new List<GeneratedLootDrop>();
        if (table == null)
        {
            return results;
        }

        for (int i = 0; i < table.stackableDrops.Count; i++)
        {
            MonsterLootTable.StackableDropRule rule = table.stackableDrops[i];
            if (rule == null || rule.itemId <= 0) continue;
            if (!Roll(rule.dropChance)) continue;

            int minAmount = Mathf.Max(1, rule.minAmount);
            int maxAmount = Mathf.Max(minAmount, rule.maxAmount);
            int amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
            results.Add(GeneratedLootDrop.Stackable(rule.itemId, amount));
        }

        for (int i = 0; i < table.weaponDrops.Count; i++)
        {
            MonsterLootTable.WeaponDropRule rule = table.weaponDrops[i];
            if (rule == null || rule.baseItemId <= 0) continue;
            if (!Roll(rule.dropChance)) continue;

            WeaponInstanceData weapon = GenerateWeapon(rule);
            results.Add(GeneratedLootDrop.Weapon(weapon));
        }

        return results;
    }

    private static WeaponInstanceData GenerateWeapon(MonsterLootTable.WeaponDropRule rule)
    {
        WeaponInstanceData weapon = new WeaponInstanceData
        {
            instanceId = Guid.NewGuid().ToString("N"),
            baseItemId = rule.baseItemId,
            baseName = string.IsNullOrWhiteSpace(rule.baseName) ? "Weapon" : rule.baseName,
            bonusAttack = RollAttack(rule),
            bonusAttackSpeedPercent = RollAttackSpeed(rule),
            effectType = RollEffect(rule, out float effectChance),
            effectChancePercent = effectChance,
        };

        return weapon;
    }

    private static int RollAttack(MonsterLootTable.WeaponDropRule rule)
    {
        if (Roll(rule.jackpotAttackChance))
        {
            return Mathf.Max(0, rule.jackpotAttackValue);
        }

        int min = Mathf.Max(0, rule.minBonusAttack);
        int max = Mathf.Max(min, rule.maxBonusAttack);
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static float RollAttackSpeed(MonsterLootTable.WeaponDropRule rule)
    {
        if (Roll(rule.jackpotAttackSpeedChance))
        {
            return Mathf.Max(0f, rule.jackpotAttackSpeedPercent);
        }

        float min = Mathf.Max(0f, rule.minBonusAttackSpeedPercent);
        float max = Mathf.Max(min, rule.maxBonusAttackSpeedPercent);
        return UnityEngine.Random.Range(min, max);
    }

    private static WeaponEffectType RollEffect(MonsterLootTable.WeaponDropRule rule, out float effectChance)
    {
        effectChance = 0f;
        if (rule.effectChances == null || rule.effectChances.Count == 0)
        {
            return WeaponEffectType.None;
        }

        float total = 0f;
        for (int i = 0; i < rule.effectChances.Count; i++)
        {
            MonsterLootTable.WeaponEffectChance opt = rule.effectChances[i];
            if (opt == null) continue;
            total += Mathf.Max(0f, opt.chance);
        }

        if (total <= 0f)
        {
            return WeaponEffectType.None;
        }

        float roll = UnityEngine.Random.value * total;
        float acc = 0f;
        for (int i = 0; i < rule.effectChances.Count; i++)
        {
            MonsterLootTable.WeaponEffectChance opt = rule.effectChances[i];
            if (opt == null) continue;

            acc += Mathf.Max(0f, opt.chance);
            if (roll > acc) continue;

            effectChance = Mathf.Clamp(opt.effectChancePercent, 0f, 100f);
            return opt.effectType;
        }

        return WeaponEffectType.None;
    }

    private static bool Roll(float chance)
    {
        return UnityEngine.Random.value <= Mathf.Clamp01(chance);
    }
}
