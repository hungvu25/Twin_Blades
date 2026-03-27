using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLootInteractor : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private Key pickupKey = Key.F;

    [Header("Detect")]
    [SerializeField] private float pickupDetectRange = 1.6f;
    [SerializeField] private LayerMask lootLayerMask = ~0;
    [SerializeField] private bool enablePickupDebugLogs = true;

    private PlayerNetworkState networkState;

    private void Awake()
    {
        networkState = GetComponent<PlayerNetworkState>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current[pickupKey].wasPressedThisFrame) return;
        if (networkState != null && !networkState.IsAlive) return;

        WorldLoot target = FindNearestLoot();
        if (target == null)
        {
            if (enablePickupDebugLogs)
            {
                Debug.Log("[Loot][Player] No loot in range for pickup.");
            }
            return;
        }

        NetworkIdentity lootIdentity = target.GetComponent<NetworkIdentity>();
        if (lootIdentity == null)
        {
            if (enablePickupDebugLogs)
            {
                Debug.LogWarning("[Loot][Player] Loot has no NetworkIdentity.");
            }
            return;
        }

        if (enablePickupDebugLogs)
        {
            Debug.Log($"[Loot][Player] Try pickup loot netId={lootIdentity.netId}");
        }

        CmdTryPickupLoot(lootIdentity.netId);
    }

    [Command]
    private void CmdTryPickupLoot(uint lootNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(lootNetId, out NetworkIdentity lootIdentity))
        {
            return;
        }

        WorldLoot loot = lootIdentity.GetComponent<WorldLoot>();
        if (loot == null)
        {
            return;
        }

        loot.TryPickupByPlayer(gameObject);
    }

    private WorldLoot FindNearestLoot()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.1f, pickupDetectRange), lootLayerMask);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        WorldLoot best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            WorldLoot loot = hit.GetComponent<WorldLoot>();
            if (loot == null)
            {
                loot = hit.GetComponentInParent<WorldLoot>();
            }

            if (loot == null) continue;

            float distance = Vector2.Distance(transform.position, loot.transform.position);
            if (distance < bestDist)
            {
                bestDist = distance;
                best = loot;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, pickupDetectRange));
    }
}
