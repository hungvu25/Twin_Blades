using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider2D))]
public class WorldLoot : NetworkBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifeTimeSeconds = 30f;
    [SerializeField] private bool enableLootDebugLogs = true;

    [Header("Pickup")]
    [SerializeField] private bool autoPickupOnTrigger = false;
    [SerializeField] private float serverPickupRange = 2f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private string itemDatabaseResourcePath = "ItemDatabase";
    [SerializeField] private Sprite fallbackStackableIcon;
    [SerializeField] private Sprite fallbackWeaponIcon;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController stackableAnimatorController;
    [SerializeField] private RuntimeAnimatorController weaponAnimatorController;
    [SerializeField] private string spawnTriggerName = "Spawn";
    [SerializeField] private string pickupTriggerName = "Pickup";

    [SyncVar(hook = nameof(OnKindChanged))] private LootDropKind kind;
    [SyncVar(hook = nameof(OnStackItemIdChanged))] private int stackItemId;
    [SyncVar] private int stackAmount;

    [SyncVar] private string weaponInstanceId;
    [SyncVar(hook = nameof(OnWeaponBaseItemIdChanged))] private int weaponBaseItemId;
    [SyncVar] private string weaponBaseName;
    [SyncVar] private int weaponBonusAttack;
    [SyncVar] private float weaponBonusAttackSpeedPercent;
    [SyncVar] private WeaponEffectType weaponEffectType;
    [SyncVar] private float weaponEffectChancePercent;

    [SyncVar] private bool picked;

    private int spawnTriggerHash;
    private int pickupTriggerHash;
    private Collider2D triggerCollider;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (lifeTimeSeconds > 0f)
        {
            Invoke(nameof(DestroyOnServer), lifeTimeSeconds);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureAnimationHashes();
        RefreshVisual();
        RefreshAnimationController();
        PlaySpawnAnimation();

        if (enableLootDebugLogs)
        {
            int id = kind == LootDropKind.Weapon ? weaponBaseItemId : stackItemId;
            string label = kind == LootDropKind.Weapon ? weaponBaseName : $"stack x{stackAmount}";
            Debug.Log($"[Loot][WorldLoot:{name}] Client start kind={kind}, itemId={id}, label={label}, icon={(iconRenderer != null && iconRenderer.sprite != null)}");
        }
    }

    [Server]
    public void InitializeStackable(int itemId, int amount)
    {
        kind = LootDropKind.Stackable;
        stackItemId = itemId;
        stackAmount = Mathf.Max(1, amount);
    }

    [Server]
    public void InitializeWeapon(WeaponInstanceData weapon)
    {
        kind = LootDropKind.Weapon;
        weaponInstanceId = weapon.instanceId;
        weaponBaseItemId = weapon.baseItemId;
        weaponBaseName = weapon.baseName;
        weaponBonusAttack = weapon.bonusAttack;
        weaponBonusAttackSpeedPercent = weapon.bonusAttackSpeedPercent;
        weaponEffectType = weapon.effectType;
        weaponEffectChancePercent = weapon.effectChancePercent;
    }

    [ServerCallback]
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoPickupOnTrigger) return;
        if (picked) return;

        if (!IsValidLootPayload())
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][WorldLoot:{name}] Ignored pickup because payload is invalid. kind={kind}, stackItemId={stackItemId}, stackAmount={stackAmount}, weaponBaseItemId={weaponBaseItemId}, weaponInstanceId={weaponInstanceId}");
            }
            return;
        }

        NetworkIdentity identity = other.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            identity = other.GetComponentInParent<NetworkIdentity>();
        }

        if (identity == null) return;

        PlayerNetworkState playerState = identity.GetComponent<PlayerNetworkState>();
        if (playerState == null)
        {
            playerState = identity.GetComponentInParent<PlayerNetworkState>();
        }

        if (playerState != null && !playerState.IsAlive) return;

        if (enableLootDebugLogs)
        {
            Debug.Log($"[Loot][WorldLoot:{name}] Pickup attempt by {identity.name}");
        }

        bool success = TryPickupByPlayer(identity.gameObject);
        if (!success)
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][WorldLoot:{name}] Pickup failed for {identity.name}");
            }
            return;
        }

        RpcPlayPickupAnimation();
        picked = true;
        DestroyOnServer();
    }

    [Server]
    public bool TryPickupByPlayer(GameObject playerObj)
    {
        if (picked) return false;
        if (playerObj == null) return false;

        if (!IsValidLootPayload())
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][WorldLoot:{name}] Server pickup rejected: invalid payload.");
            }
            return false;
        }

        float maxRange = Mathf.Max(0.1f, serverPickupRange);
        float distance = Vector2.Distance(transform.position, playerObj.transform.position);
        if (distance > maxRange)
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][WorldLoot:{name}] Server pickup rejected: out of range ({distance:0.##} > {maxRange:0.##}).");
            }
            return false;
        }

        bool success = TryGrantLoot(playerObj);
        if (!success)
        {
            if (enableLootDebugLogs)
            {
                Debug.LogWarning($"[Loot][WorldLoot:{name}] Server pickup rejected: grant failed.");
            }
            return false;
        }

        if (enableLootDebugLogs)
        {
            Debug.Log($"[Loot][WorldLoot:{name}] Server pickup success by {playerObj.name}");
        }

        RpcPlayPickupAnimation();
        picked = true;
        DestroyOnServer();
        return true;
    }

    [Server]
    private bool TryGrantLoot(GameObject playerObj)
    {
        if (playerObj == null) return false;

        if (kind == LootDropKind.Stackable)
        {
            NetworkInventory inventory = playerObj.GetComponent<NetworkInventory>();
            if (inventory == null)
            {
                inventory = playerObj.GetComponentInParent<NetworkInventory>();
            }

            if (inventory == null) return false;
            return inventory.TryAddItemServer(stackItemId, stackAmount);
        }

        PlayerWeaponInventory weaponInventory = playerObj.GetComponent<PlayerWeaponInventory>();
        if (weaponInventory == null)
        {
            weaponInventory = playerObj.GetComponentInParent<PlayerWeaponInventory>();
        }

        if (weaponInventory == null) return false;

        WeaponInstanceData data = new WeaponInstanceData
        {
            instanceId = weaponInstanceId,
            baseItemId = weaponBaseItemId,
            baseName = weaponBaseName,
            bonusAttack = weaponBonusAttack,
            bonusAttackSpeedPercent = weaponBonusAttackSpeedPercent,
            effectType = weaponEffectType,
            effectChancePercent = weaponEffectChancePercent,
        };

        return weaponInventory.TryAddWeaponServer(data);
    }

    [Server]
    private void DestroyOnServer()
    {
        if (enableLootDebugLogs)
        {
            Debug.Log($"[Loot][WorldLoot:{name}] Destroy on server.");
        }

        if (gameObject != null)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();

        if (iconRenderer == null)
        {
            iconRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        EnsureAnimationHashes();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnKindChanged(LootDropKind oldKind, LootDropKind newKind)
    {
        RefreshVisual();
        RefreshAnimationController();
        PlaySpawnAnimation();
    }

    private void OnStackItemIdChanged(int oldItemId, int newItemId)
    {
        RefreshVisual();
    }

    private void OnWeaponBaseItemIdChanged(int oldItemId, int newItemId)
    {
        RefreshVisual();
    }

    [ClientRpc]
    private void RpcPlayPickupAnimation()
    {
        if (animator == null) return;
        if (pickupTriggerHash == 0) return;

        animator.SetTrigger(pickupTriggerHash);
    }

    private void RefreshVisual()
    {
        if (iconRenderer == null)
        {
            return;
        }

        Sprite icon = null;
        int lookupItemId = kind == LootDropKind.Weapon ? weaponBaseItemId : stackItemId;
        if (lookupItemId > 0)
        {
            ItemDatabase db = ResolveItemDatabase();
            if (db != null && db.TryGetItem(lookupItemId, out ItemDefinition def) && def != null)
            {
                icon = def.Icon;
            }
        }

        if (icon == null)
        {
            icon = kind == LootDropKind.Weapon ? fallbackWeaponIcon : fallbackStackableIcon;
        }

        iconRenderer.sprite = icon;
        iconRenderer.enabled = icon != null;

        if (enableLootDebugLogs)
        {
            int id = kind == LootDropKind.Weapon ? weaponBaseItemId : stackItemId;
            Debug.Log($"[Loot][WorldLoot:{name}] RefreshVisual itemId={id}, iconFound={(icon != null)}");
        }
    }

    private void RefreshAnimationController()
    {
        if (animator == null)
        {
            return;
        }

        RuntimeAnimatorController targetController = GetPerItemAnimatorController();
        if (targetController == null)
        {
            targetController = kind == LootDropKind.Weapon
                ? weaponAnimatorController
                : stackableAnimatorController;
        }

        if (targetController != null && animator.runtimeAnimatorController != targetController)
        {
            animator.runtimeAnimatorController = targetController;
        }
    }

    private void PlaySpawnAnimation()
    {
        if (animator == null) return;
        if (spawnTriggerHash == 0) return;

        animator.SetTrigger(spawnTriggerHash);
    }

    private void EnsureAnimationHashes()
    {
        spawnTriggerHash = string.IsNullOrWhiteSpace(spawnTriggerName)
            ? 0
            : Animator.StringToHash(spawnTriggerName);

        pickupTriggerHash = string.IsNullOrWhiteSpace(pickupTriggerName)
            ? 0
            : Animator.StringToHash(pickupTriggerName);
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

    private RuntimeAnimatorController GetPerItemAnimatorController()
    {
        int lookupItemId = kind == LootDropKind.Weapon ? weaponBaseItemId : stackItemId;
        if (lookupItemId <= 0)
        {
            return null;
        }

        ItemDatabase db = ResolveItemDatabase();
        if (db == null)
        {
            return null;
        }

        if (!db.TryGetItem(lookupItemId, out ItemDefinition def) || def == null)
        {
            return null;
        }

        return def.LootAnimatorController;
    }

    private bool IsValidLootPayload()
    {
        if (kind == LootDropKind.Stackable)
        {
            return stackItemId > 0 && stackAmount > 0;
        }

        return weaponBaseItemId > 0 && !string.IsNullOrWhiteSpace(weaponInstanceId);
    }
}
