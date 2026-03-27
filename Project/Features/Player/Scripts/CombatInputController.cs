using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CombatInputController : NetworkBehaviour
{
    private enum AttackType
    {
        Normal = 0,
        Charge = 1,
        Full = 2,
    }

    private static readonly int AttackDirXHash = Animator.StringToHash("AttackDirX");
    private static readonly int AttackDirYHash = Animator.StringToHash("AttackDirY");

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Direct Attack States")]
    [SerializeField] private string normalStateName = "AttackSM.Normal1";
    [SerializeField] private string chargeStateName = "AttackSM.Charge1";
    [SerializeField] private string fullStateName = "AttackSM.Full3";
    [SerializeField] private float attackCrossFadeSeconds = 0f;
    [SerializeField] private bool scaleAttackAnimationWithAttackSpeed = true;
    [SerializeField] private float minAttackAnimationSpeed = 0.6f;
    [SerializeField] private float maxAttackAnimationSpeed = 3f;

    [Header("Input Bindings")]
    [SerializeField] private Key normalAttackKey = Key.Q;
    [SerializeField] private Key chargeAttackKey = Key.E;
    [SerializeField] private Key fullSelectKey = Key.R;

    [Header("Charge (Hold E)")]
    [SerializeField] private float chargeHoldSeconds = 0.5f;

    [Header("Cooldown")]
    [SerializeField] private float normalCooldownSeconds = 0.2f;
    [SerializeField] private float chargeCooldownSeconds = 2f;
    [SerializeField] private float fullCooldownSeconds = 6f;

    [Header("Skill Damage")]
    [SerializeField] private int normalSkillDamage = 20;
    [SerializeField] private int chargeSkillDamage = 35;
    [SerializeField] private int fullSkillDamage = 60;

    [Header("Skill Hit Detection")]
    [SerializeField] private float normalHitRadius = 1.1f;
    [SerializeField] private float normalHitForwardOffset = 0.6f;
    [SerializeField] private float chargeHitRadius = 1.8f;
    [SerializeField] private float chargeHitForwardOffset = 0f;
    [SerializeField] private float fullHitRadius = 1.4f;
    [SerializeField] private float fullHitForwardOffset = 0.7f;
    [SerializeField] private LayerMask monsterLayerMask = ~0;

    [Header("Safety")]
    [SerializeField] private float attackLockTimeoutSeconds = 1.5f;

    private PlayerMovement movement;
    private PlayerNetworkState networkState;
    private PlayerStats stats;

    private bool comboWindowOpen;
    private bool isAttacking;
    private bool queueNext;
    private AttackType currentAttackType = AttackType.Normal;

    private bool isCharging;
    private float chargeStartTime;
    private bool chargeImpactPending;

    private float attackLockUntil;

    private float nextNormalReadyTime;
    private float nextChargeReadyTime;
    private float nextFullReadyTime;

    private int normalStateHash;
    private int chargeStateHash;
    private int fullStateHash;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        movement = GetComponent<PlayerMovement>();
        networkState = GetComponent<PlayerNetworkState>();
        stats = GetComponent<PlayerStats>();

        normalStateHash = Animator.StringToHash(normalStateName);
        chargeStateHash = Animator.StringToHash(chargeStateName);
        fullStateHash = Animator.StringToHash(fullStateName);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
    }

    private void OnDestroy()
    {
        if (!isLocalPlayer) return;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (animator == null || Keyboard.current == null) return;
        if (networkState != null && !networkState.IsAlive) return;

        HandleNormalInput();
        HandleChargeInput();
        HandleFullInput();
        TryAutoRecoverAttackLock();
    }

    // Animation Event: đặt ở frame mở cửa sổ nối combo
    public void OpenComboWindow()
    {
        comboWindowOpen = true;
    }

    // Animation Event: đặt ở frame đóng cửa sổ nối combo
    public void CloseComboWindow()
    {
        comboWindowOpen = false;
        queueNext = false;
    }

    // Animation Event: gọi ở cuối mỗi đòn để dọn trạng thái
    public void OnAttackFinished()
    {
        bool hadQueue = queueNext;
        AttackType finishedAttackType = currentAttackType;

        isAttacking = false;
        comboWindowOpen = false;
        queueNext = false;
        chargeImpactPending = false;
        attackLockUntil = 0f;
        animator.speed = 1f;

        // Khi người chơi đã bấm Q trong lúc đang đánh, tung đòn kế ngay ở cuối clip hiện tại.
        if (hadQueue && finishedAttackType == AttackType.Normal)
        {
            FireAttack(AttackType.Normal, 1);
            return;
        }
    }

    private void HandleNormalInput()
    {
        if (!IsPressedThisFrame(normalAttackKey)) return;
        if (isCharging) return;
        if (Time.time < nextNormalReadyTime) return;

        if (!isAttacking)
        {
            FireAttack(AttackType.Normal, 1);
            return;
        }

        if (currentAttackType != AttackType.Normal) return;
        if (!comboWindowOpen) return;

        queueNext = true;
    }

    private void HandleChargeInput()
    {
        if (isAttacking) return;

        if (IsPressedThisFrame(chargeAttackKey))
        {
            isCharging = true;
            chargeStartTime = Time.time;
        }

        if (!IsReleasedThisFrame(chargeAttackKey)) return;
        if (!isCharging) return;
        if (Time.time < nextChargeReadyTime) return;

        isCharging = false;
        float holdDuration = Time.time - chargeStartTime;

        if (holdDuration < chargeHoldSeconds)
        {
            return;
        }

        FireAttack(AttackType.Charge, 1);
    }

    private void HandleFullInput()
    {
        if (isAttacking) return;
        if (!IsPressedThisFrame(fullSelectKey)) return;
        if (Time.time < nextFullReadyTime) return;

        FireAttack(AttackType.Full, 1);
    }

    private void FireAttack(AttackType attackType, int step)
    {
        step = NormalizeAttackStep(attackType, step);

        Vector2 facingDirection = GetFacingDirection();
        float attackAnimSpeed = GetAttackAnimationSpeedMultiplier();
        if (!PlayAttackVisual(attackType, facingDirection, attackAnimSpeed))
        {
            return;
        }

        if (NetworkClient.active && NetworkClient.ready)
        {
            CmdSyncAttackAnimation((int)attackType, facingDirection, attackAnimSpeed);
        }

        if (attackType == AttackType.Charge)
        {
            chargeImpactPending = true;
        }
        else
        {
            RequestSkillDamage(attackType, facingDirection);
        }

        StartSkillCooldown(attackType);

        isAttacking = true;
        queueNext = false;
        comboWindowOpen = false;
        attackLockUntil = Time.time + Mathf.Max(0.3f, attackLockTimeoutSeconds);
    }

    private bool PlayAttackVisual(AttackType attackType, Vector2 facingDirection, float animSpeed)
    {
        currentAttackType = attackType;
        animator.SetFloat(AttackDirXHash, facingDirection.x);
        animator.SetFloat(AttackDirYHash, facingDirection.y);
        animator.speed = animSpeed;

        int stateHash = GetAttackStateHash(attackType);
        if (stateHash == 0)
        {
            return false;
        }

        // Đánh trực tiếp vào state để tránh kẹt ở state trung gian AttackEntry.
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, attackCrossFadeSeconds), 0, 0f);
        return true;
    }

    [Command]
    private void CmdSyncAttackAnimation(int attackTypeRaw, Vector2 facingDirection, float animSpeed)
    {
        if (networkState != null && !networkState.IsAlive) return;

        int clampedType = Mathf.Clamp(attackTypeRaw, 0, 2);
        RpcSyncAttackAnimation(clampedType, facingDirection, animSpeed);
    }

    [ClientRpc]
    private void RpcSyncAttackAnimation(int attackTypeRaw, Vector2 facingDirection, float animSpeed)
    {
        if (isLocalPlayer) return;
        if (animator == null) return;

        AttackType attackType = (AttackType)Mathf.Clamp(attackTypeRaw, 0, 2);
        float safeSpeed = Mathf.Clamp(animSpeed, 0.1f, 5f);
        if (!PlayAttackVisual(attackType, facingDirection, safeSpeed))
        {
            return;
        }

        isAttacking = true;
        queueNext = false;
        comboWindowOpen = false;
        chargeImpactPending = false;
        attackLockUntil = Time.time + Mathf.Max(0.3f, attackLockTimeoutSeconds);
    }

    private int NormalizeAttackStep(AttackType attackType, int step)
    {
        // Cấu hình mới chỉ còn 1 state cho mỗi loại đòn.
        return 1;
    }

    private int GetAttackStateHash(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Normal:
                return normalStateHash;
            case AttackType.Charge:
                return chargeStateHash;
            case AttackType.Full:
                return fullStateHash;
            default:
                return 0;
        }
    }

    private int GetSkillDamage(AttackType attackType)
    {
        int baseDamage;
        switch (attackType)
        {
            case AttackType.Normal:
                baseDamage = Mathf.Max(0, normalSkillDamage);
                break;
            case AttackType.Charge:
                baseDamage = Mathf.Max(0, chargeSkillDamage);
                break;
            case AttackType.Full:
                baseDamage = Mathf.Max(0, fullSkillDamage);
                break;
            default:
                return 0;
        }

        if (stats != null)
            return stats.CalculateSkillDamage(baseDamage);

        return baseDamage;
    }

    // Animation Event: đặt vào đúng frame va chạm của Charge để gây dame AoE.
    public void OnChargeImpactEvent()
    {
        if (!isLocalPlayer) return;
        if (!isAttacking) return;
        if (!chargeImpactPending) return;
        if (currentAttackType != AttackType.Charge) return;

        chargeImpactPending = false;

        Vector2 facingDirection = GetFacingDirection();
        RequestSkillDamage(AttackType.Charge, facingDirection);
    }

    private void StartSkillCooldown(AttackType attackType)
    {
        float normalCd = stats != null ? stats.GetCooldown(normalCooldownSeconds) : Mathf.Max(0f, normalCooldownSeconds);
        float chargeCd = stats != null ? stats.GetCooldown(chargeCooldownSeconds) : Mathf.Max(0f, chargeCooldownSeconds);
        float fullCd = stats != null ? stats.GetCooldown(fullCooldownSeconds) : Mathf.Max(0f, fullCooldownSeconds);

        switch (attackType)
        {
            case AttackType.Normal:
                nextNormalReadyTime = Time.time + normalCd;
                break;
            case AttackType.Charge:
                nextChargeReadyTime = Time.time + chargeCd;
                break;
            case AttackType.Full:
                nextFullReadyTime = Time.time + fullCd;
                break;
        }
    }

    private float GetSkillHitRadius(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Normal:
                return Mathf.Max(0f, normalHitRadius);
            case AttackType.Charge:
                return Mathf.Max(0f, chargeHitRadius);
            case AttackType.Full:
                return Mathf.Max(0f, fullHitRadius);
            default:
                return 0f;
        }
    }

    private float GetSkillHitForwardOffset(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Normal:
                return normalHitForwardOffset;
            case AttackType.Charge:
                return chargeHitForwardOffset;
            case AttackType.Full:
                return fullHitForwardOffset;
            default:
                return 0f;
        }
    }

    private void RequestSkillDamage(AttackType attackType, Vector2 facingDirection)
    {
        if (!isLocalPlayer) return;

        if (isServer)
        {
            ApplySkillDamageServer(attackType, facingDirection);
            return;
        }

        if (NetworkClient.active && NetworkClient.ready)
        {
            CmdApplySkillDamage((int)attackType, facingDirection);
        }
    }

    [Command]
    private void CmdApplySkillDamage(int attackTypeRaw, Vector2 facingDirection)
    {
        AttackType attackType = (AttackType)Mathf.Clamp(attackTypeRaw, 0, 2);
        ApplySkillDamageServer(attackType, facingDirection);
    }

    [Server]
    private void ApplySkillDamageServer(AttackType attackType, Vector2 facingDirection)
    {
        int damage = GetSkillDamage(attackType);
        if (damage <= 0) return;

        float hitRadius = GetSkillHitRadius(attackType);
        if (hitRadius <= 0f) return;

        if (facingDirection.sqrMagnitude <= 0.0001f)
            facingDirection = Vector2.down;
        facingDirection.Normalize();

        float forwardOffset = GetSkillHitForwardOffset(attackType);
        Vector2 center = (Vector2)transform.position + facingDirection * forwardOffset;
        int mask = monsterLayerMask.value;
        if (mask == 0)
            mask = ~0;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, mask);
        HashSet<MonsterAI> damagedMonsters = new HashSet<MonsterAI>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            MonsterAI monster = hit.GetComponent<MonsterAI>();
            if (monster == null)
                monster = hit.GetComponentInParent<MonsterAI>();
            if (monster == null) continue;
            if (!monster.IsAlive) continue;
            if (!damagedMonsters.Add(monster)) continue;

            monster.TakeDamage(damage);
        }
    }

    private bool IsPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return keyboard[key].wasPressedThisFrame;
    }

    private bool IsReleasedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return keyboard[key].wasReleasedThisFrame;
    }

    private void TryAutoRecoverAttackLock()
    {
        if (!isAttacking) return;
        if (attackLockUntil <= 0f) return;
        if (Time.time <= attackLockUntil) return;

        // Fallback khi clip thiếu Animation Event OnAttackFinished.
        isAttacking = false;
        queueNext = false;
        comboWindowOpen = false;
        attackLockUntil = 0f;
        animator.speed = 1f;
    }

    private float GetAttackAnimationSpeedMultiplier()
    {
        if (!scaleAttackAnimationWithAttackSpeed)
            return 1f;

        float statMultiplier = stats != null ? stats.AttackSpeed : 1f;
        return Mathf.Clamp(statMultiplier, Mathf.Max(0.1f, minAttackAnimationSpeed), Mathf.Max(minAttackAnimationSpeed, maxAttackAnimationSpeed));
    }

    private Vector2 GetFacingDirection()
    {
        if (movement != null && movement.LastMoveDirection.sqrMagnitude > 0.0001f)
        {
            return movement.LastMoveDirection;
        }

        if (networkState != null && networkState.LastMoveDirection.sqrMagnitude > 0.0001f)
        {
            return networkState.LastMoveDirection;
        }

        return Vector2.down;
    }

}
