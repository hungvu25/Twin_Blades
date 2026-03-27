using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Core Stats")]
    [SerializeField] private int attack = 10;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float critChance = 0.15f;
    [SerializeField] private int maxHp = 100;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Movement")]
    [SerializeField] private float runSpeedMultiplier = 1.6f;

    [Header("Crit")]
    [SerializeField] private float critDamageMultiplier = 2f;

    public int Attack => Mathf.Max(0, attack);
    public float AttackSpeed => Mathf.Max(0.1f, attackSpeed);
    public float CritChance => Mathf.Clamp01(critChance);
    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);

    public float GetWalkSpeed()
    {
        return MoveSpeed;
    }

    public float GetRunSpeed()
    {
        return MoveSpeed * Mathf.Max(1f, runSpeedMultiplier);
    }

    public float GetCooldown(float baseCooldown)
    {
        float safeBase = Mathf.Max(0f, baseCooldown);
        return safeBase / AttackSpeed;
    }

    public int CalculateSkillDamage(int baseDamage)
    {
        int flatDamage = Mathf.Max(0, baseDamage) + Attack;
        bool isCrit = Random.value <= CritChance;
        if (!isCrit)
            return flatDamage;

        float critDamage = flatDamage * Mathf.Max(1f, critDamageMultiplier);
        return Mathf.Max(flatDamage, Mathf.RoundToInt(critDamage));
    }
}
