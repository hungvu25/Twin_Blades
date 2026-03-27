using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

public class PlayerController : NetworkBehaviour
{
    [Header("Chỉ số")]
    public int maxHealth = 100;
    [Header("UI")]
    [SerializeField] private string combatSceneName = "BattleScene";

    // SyncVar giúp tự động đồng bộ biến này từ Server về tất cả Client
    // hook sẽ gọi hàm OnHealthChanged mỗi khi currentHealth thay đổi
    [SyncVar(hook = nameof(OnHealthChanged))]
    private int currentHealth;

    private HealthBar healthBar;
    private GameObject healthBarObject;
    private PlayerStats stats;

    [SerializeField] private float respawnDelaySeconds = 2f;
    [SerializeField] private string baseSpawnTag = "BaseSpawn";
    [SerializeField] private Transform baseSpawnPoint;
    [SerializeField] private string baseSpawnObjectName = "SpawnPoint";

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            maxHealth = stats.MaxHp;
        }
    }

    void Start()
    {
        if (isServer)
        {
            currentHealth = maxHealth;
        }

        // Tìm thanh máu trên UI (Chỉ máy của người chơi đó mới cần hiện HUD)
        if (isLocalPlayer)
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshHealthBarBindingAndVisibility();
        }
    }

    private void OnDestroy()
    {
        if (isLocalPlayer)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Hàm này chạy trên Server khi quái chém trúng
    [Server]
    public void TakeDamage(int amount)
    {
        if ( amount <= 0) return;
        PlayerNetworkState state = GetComponent<PlayerNetworkState>();
        if ( state != null && !state.IsAlive) return;
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);
        if ( currentHealth <= 0)
        {
            state.IsAlive = false;
            state.IsMoving = false;
            state.IsRunning = false;
            StartCoroutine(ServerRespawnRoutine());
        }
    }

    // Hàm Hook này tự động chạy trên máy Client khi máu thay đổi
    void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (isLocalPlayer && healthBar != null)
        {
            healthBar.UpdateHealth(newHealth, maxHealth);
        }
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        RefreshHealthBarBindingAndVisibility();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshHealthBarBindingAndVisibility();
    }

    private void RefreshHealthBarBindingAndVisibility()
    {
        if (!isLocalPlayer) return;

        if (healthBarObject == null)
        {
            HealthBar[] healthBars = FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (HealthBar hb in healthBars)
            {
                if (hb == null || hb.gameObject == null) continue;
                if (hb.gameObject.name != "HealthBar_Player") continue;

                healthBar = hb;
                healthBarObject = hb.gameObject;
                break;
            }
        }

        if (healthBarObject == null) return;

        bool shouldShow = SceneManager.GetActiveScene().name == combatSceneName;
        healthBarObject.SetActive(shouldShow);

        if (shouldShow && healthBar != null)
        {
            healthBar.UpdateHealth(currentHealth, maxHealth);
        }
    }

    [Server]
    private IEnumerator ServerRespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelaySeconds);

        Transform spawn = FindBaseSpawn();
        if (spawn != null)
        {
            transform.position = spawn.position; // NetworkTransform sẽ sync vị trí
        }
        else
        {
            Debug.LogWarning("[PlayerController] Khong tim thay diem respawn. Dat tag BaseSpawn hoac ten BaseSpawnPoint trong scene.");
        }

        currentHealth = maxHealth;

        PlayerNetworkState state = GetComponent<PlayerNetworkState>();
        if (state != null)
        {
            state.IsAlive = true;
            state.IsMoving = false;
            state.IsRunning = false;
        }
    }

    [Server]
    private Transform FindBaseSpawn()
    {
        // NOTE: Prefab reference khong giu duoc object trong scene,
        // nen uu tien tim diem spawn bang Tag/Name runtime.
        if (!string.IsNullOrWhiteSpace(baseSpawnTag))
        {
            try
            {
                GameObject tagged = GameObject.FindWithTag(baseSpawnTag);
                if (tagged != null && tagged.scene.IsValid())
                    return tagged.transform;
            }
            catch (UnityException)
            {
                // Tag chua duoc tao trong Project Settings > Tags and Layers.
            }
        }

        if (baseSpawnPoint != null)
            return baseSpawnPoint;

        NetworkStartPosition[] startPositions = FindObjectsByType<NetworkStartPosition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (NetworkStartPosition start in startPositions)
        {
            if (start == null) continue;
            if (!start.gameObject.scene.IsValid()) continue;
            return start.transform;
        }

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
            if (t != null && t.name == baseSpawnObjectName && t.gameObject.scene.IsValid())
                return t;
        return null;
    }
}