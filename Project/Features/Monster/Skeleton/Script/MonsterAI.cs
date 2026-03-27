using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class MonsterAI : NetworkBehaviour
{
    [Header("Cấu hình máu")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float destroyAfterDeathSeconds = 2f;

    [Header("Loot")]
    [SerializeField] private MonsterLootTable lootTable;
    [SerializeField] private WorldLoot worldLootPrefab;
    [SerializeField] private float lootScatterRadius = 0.6f;
    [SerializeField] private bool enableLootDebugLogs = true;

    [Header("Cấu hình di chuyển")]
    public float moveSpeed = 3f;
    public float detectionRange = 10f;
    public float stoppingDistance = 1.2f;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private int currentHealth;

    [SyncVar]
    private bool isDead;

    private Rigidbody2D rb;
    private Collider2D selfCollider;
    private Transform targetPlayer;
    private MonsterAnimationGeneric animController; // Thêm dòng này
    private MonsterHealthBarUI healthBarUI;

    public bool IsAlive => !isDead;
    public int CurrentHealth => currentHealth;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        selfCollider = GetComponent<Collider2D>();
        animController = GetComponent<MonsterAnimationGeneric>(); // Thêm dòng này
        healthBarUI = GetComponent<MonsterHealthBarUI>();
        if (healthBarUI == null)
            healthBarUI = gameObject.AddComponent<MonsterHealthBarUI>();

        if (isServer)
        {
            currentHealth = Mathf.Max(1, maxHealth);
            isDead = false;
        }

        if (healthBarUI != null)
            healthBarUI.Initialize(currentHealth, Mathf.Max(1, maxHealth));
        
        if (isServer)
        {
            InvokeRepeating(nameof(FindNearestPlayer), 0f, 0.5f);
        }
    }

    [ServerCallback]
    void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (targetPlayer == null) 
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (Time.time < actionLockUntil)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsTargetAlive(targetPlayer))
        {
            targetPlayer = null;
            rb.linearVelocity = Vector2.zero;
            isAttackPending = false;
            pendingAttackTarget = null;
            return;
        }

        // Luôn cập nhật hướng nhìn theo vị trí player để tránh cảm giác đi lùi.
        float deltaX = targetPlayer.position.x - transform.position.x;
        if (animController != null && Mathf.Abs(deltaX) > 0.05f)
        {
            animController.SetMoveDirection(deltaX);
        }

        float distance = GetDistanceToTarget(targetPlayer);
        float effectiveStopDistance = Mathf.Max(stoppingDistance, attackRange);

        // 1. Logic di chuyển
        if (distance < detectionRange && distance > effectiveStopDistance)
        {
            Vector2 direction = GetDirectionToTarget(targetPlayer);
            rb.linearVelocity = direction * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        // 2. Logic Tấn công
        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            AttackPlayer();
            nextAttackTime = Time.time + attackCooldown;
        }

        // Tránh kẹt trạng thái nếu clip không phát Animation Event.
        if (isAttackPending && Time.time >= pendingAttackExpireTime)
        {
            isAttackPending = false;
            pendingAttackTarget = null;
        }
    }
    
    [Server]
    void FindNearestPlayer()
    {
        if (isDead)
        {
            targetPlayer = null;
            return;
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (GameObject p in players)
        {
            if (!IsTargetAlive(p.transform)) continue;

            float d = Vector2.Distance(transform.position, p.transform.position);
            if (d < closestDistance)
            {
                closestDistance = d;
                closestPlayer = p.transform;
            }
        }
        targetPlayer = closestPlayer;
    }

    [Header("Cấu hình tấn công")]
    public float attackRange = 1.5f; // Khoảng cách để quái ra đòn
    public float attackCooldown = 1.2f; // Thời gian chờ giữa 2 lần chém
    public float attackEventTimeout = 1.5f; // Nếu không nhận Animation Event, tự hủy đòn chờ
    public float hitReactionLockSeconds = 0.2f; // Khóa AI ngắn khi bị trúng đòn
    public int attackDamage = 10;
    private float nextAttackTime;
    private bool isAttackPending;
    private Transform pendingAttackTarget;
    private float pendingAttackExpireTime;
    private float actionLockUntil;



    [Server]
    void AttackPlayer()
    {
        if (isDead) return;
        if (Time.time < actionLockUntil) return;
        if (targetPlayer == null) return;
        if (!IsTargetAlive(targetPlayer)) return;
        if (isAttackPending) return;

        isAttackPending = true;
        pendingAttackTarget = targetPlayer;
        pendingAttackExpireTime = Time.time + Mathf.Max(attackEventTimeout, 0.1f);

        // 1. Chạy animation chém cho toàn bộ client.
        if (animController != null)
            animController.TriggerAttackAnimation();
    }
    
    [Server]
    public void ResolveAttackFromAnimationEvent()
    {
        if (isDead) return;
        if (!isAttackPending) return;

        Transform attackTarget = pendingAttackTarget;
        isAttackPending = false;
        pendingAttackTarget = null;

        if (attackTarget == null) return;
        if (!IsTargetAlive(attackTarget)) return;

        float distanceToTarget = GetDistanceToTarget(attackTarget);
        if (distanceToTarget > attackRange) return;

        PlayerNetworkState pState = attackTarget.GetComponent<PlayerNetworkState>();
        if (pState == null) pState = attackTarget.GetComponentInParent<PlayerNetworkState>();
        if (pState != null && !pState.IsAlive) return;
        PlayerController player = attackTarget.GetComponent<PlayerController>();
        if (player == null)
            player = attackTarget.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            player.TakeDamage(attackDamage);
            Debug.Log($"Quái đã gây {attackDamage} sát thương!");
        }
    }

    [Server]
    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        // Bị đánh thì hủy đòn đang chờ để tránh Attack đè lên hit/death.
        isAttackPending = false;
        pendingAttackTarget = null;
        pendingAttackExpireTime = 0f;
        rb.linearVelocity = Vector2.zero;

        float reactionLock = Mathf.Max(0f, hitReactionLockSeconds);
        actionLockUntil = Mathf.Max(actionLockUntil, Time.time + reactionLock);
        nextAttackTime = Mathf.Max(nextAttackTime, actionLockUntil);

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth > 0)
        {
            if (animController != null)
                animController.TriggerTakeDamageAnimation();
            return;
        }

        DieServer();
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (healthBarUI != null)
            healthBarUI.SetHealth(newHealth, Mathf.Max(1, maxHealth));
    }

    [Server]
    private void DieServer()
    {
        if (isDead) return;

        isDead = true;
        actionLockUntil = float.MaxValue;
        if (animController != null)
            animController.TriggerDeathAnimation();

        SpawnLootDropsServer();

        targetPlayer = null;
        isAttackPending = false;
        pendingAttackTarget = null;
        rb.linearVelocity = Vector2.zero;

        if (selfCollider != null)
            selfCollider.enabled = false;

        if (healthBarUI != null)
            healthBarUI.SetVisible(false);

        if (destroyAfterDeathSeconds > 0f)
            Invoke(nameof(DestroyOnServer), destroyAfterDeathSeconds);
        else
            DestroyOnServer();
    }

    [Server]
    private void DestroyOnServer()
    {
        if (gameObject != null)
            NetworkServer.Destroy(gameObject);
    }

    [Server]
    private void SpawnLootDropsServer()
    {
        if (lootTable == null || worldLootPrefab == null)
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][Monster:{name}] Missing lootTable or worldLootPrefab. table={(lootTable != null)}, prefab={(worldLootPrefab != null)}");
            }
            return;
        }

        List<GeneratedLootDrop> drops = LootGenerator.RollDrops(lootTable);
        if (drops == null || drops.Count == 0)
        {
            if (enableLootDebugLogs)
            {
                Debug.Log($"[Loot][Monster:{name}] Roll result: no drops.");
            }
            return;
        }

        if (enableLootDebugLogs)
        {
            Debug.Log($"[Loot][Monster:{name}] Roll result count={drops.Count}");
        }

        for (int i = 0; i < drops.Count; i++)
        {
            GeneratedLootDrop drop = drops[i];
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, lootScatterRadius);
            Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0f);

            WorldLoot loot = Instantiate(worldLootPrefab, spawnPos, Quaternion.identity);
            if (loot == null)
            {
                if (enableLootDebugLogs)
                {
                    Debug.LogWarning($"[Loot][Monster:{name}] Failed to instantiate WorldLoot prefab.");
                }
                continue;
            }

            if (drop.kind == LootDropKind.Stackable)
            {
                loot.InitializeStackable(drop.itemId, drop.amount);
            }
            else
            {
                loot.InitializeWeapon(drop.weapon);
            }

            NetworkServer.Spawn(loot.gameObject);

            if (enableLootDebugLogs)
            {
                Debug.Log($"[Loot][Monster:{name}] Spawned drop {DescribeDrop(drop)} at {spawnPos}");
            }
        }
    }

    private static string DescribeDrop(GeneratedLootDrop drop)
    {
        if (drop.kind == LootDropKind.Stackable)
        {
            return $"Stackable(id={drop.itemId}, amount={drop.amount})";
        }

        WeaponInstanceData w = drop.weapon;
        return $"Weapon(id={w.baseItemId}, name={w.baseName}, atk+{w.bonusAttack}, aspd+{w.bonusAttackSpeedPercent:0.##}%, effect={w.effectType}@{w.effectChancePercent:0.##}%)";
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

    private Collider2D GetTargetCollider(Transform target)
    {
        if (target == null) return null;

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();
        if (targetCollider == null)
            targetCollider = target.GetComponentInParent<Collider2D>();

        return targetCollider;
    }

    private float GetDistanceToTarget(Transform target)
    {
        if (target == null) return Mathf.Infinity;

        Collider2D targetCollider = GetTargetCollider(target);
        if (selfCollider != null && targetCollider != null)
        {
            Vector2 selfPoint = selfCollider.ClosestPoint(targetCollider.bounds.center);
            Vector2 targetPoint = targetCollider.ClosestPoint(selfPoint);
            return Vector2.Distance(selfPoint, targetPoint);
        }

        return Vector2.Distance(transform.position, target.position);
    }

    private Vector2 GetDirectionToTarget(Transform target)
    {
        if (target == null) return Vector2.zero;

        Collider2D targetCollider = GetTargetCollider(target);
        if (selfCollider != null && targetCollider != null)
        {
            Vector2 from = selfCollider.ClosestPoint(targetCollider.bounds.center);
            Vector2 to = targetCollider.ClosestPoint(from);
            return (to - from).normalized;
        }

        return ((Vector2)target.position - rb.position).normalized;
    }

    private bool IsTargetAlive(Transform target)
    {
        if (target == null) return false;

        PlayerNetworkState state = target.GetComponent<PlayerNetworkState>();
        if (state == null)
            state = target.GetComponentInParent<PlayerNetworkState>();

        return state == null || state.IsAlive;
    }
}