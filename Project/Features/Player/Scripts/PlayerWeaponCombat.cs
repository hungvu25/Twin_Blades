using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeaponCombat : NetworkBehaviour
{
    [System.Serializable]
    public class WeaponDefinition
    {
        public string id = "Sword";
        public GameObject weaponPrefab;
        public Sprite sprite;
        public int damage = 10;
        public float attackCooldown = 0.35f;
        public float swingDuration = 0.12f;
        public float swingArc = 90f;
        public float holdDistance = 0.32f;
        public float idleAngleOffset = -90f;
    }

    [Header("Weapon Slots")]
    [SerializeField] private WeaponDefinition[] weapons;

    [Header("References")]
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private SpriteRenderer weaponRenderer;

    [Header("Input")]
    [SerializeField] private Key attackKey = Key.J;

    [SyncVar(hook = nameof(OnWeaponIndexChanged))]
    private int currentWeaponIndex;

    private PlayerMovement movement;
    private PlayerNetworkState networkState;
    private SpriteRenderer playerRenderer;
    private GameObject spawnedWeaponVisual;
    private SpriteRenderer[] activeWeaponRenderers;

    private bool isSwinging;
    private float nextAttackTime;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        networkState = GetComponent<PlayerNetworkState>();
        playerRenderer = GetComponent<SpriteRenderer>();

        EnsureWeaponObjects();
        ApplyWeaponVisual(currentWeaponIndex);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureWeaponObjects();
        ApplyWeaponVisual(currentWeaponIndex);
    }

    private void Update()
    {
        UpdateIdleWeaponPose();

        if (!isLocalPlayer) return;
        if (Keyboard.current == null) return;

        HandleEquipInput();
        HandleAttackInput();
    }

    private void HandleEquipInput()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) TryEquipWeapon(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) TryEquipWeapon(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) TryEquipWeapon(2);
    }

    private void HandleAttackInput()
    {
        bool mouseAttack = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool keyAttack = Keyboard.current[attackKey].wasPressedThisFrame;
        if (!mouseAttack && !keyAttack) return;

        if (isSwinging) return;
        if (Time.time < nextAttackTime) return;
        Vector2 attackDir = GetFacingDirection();

        // Cho phản hồi ngay cả khi vừa chuyển scene và client chưa ready.
        if (!NetworkClient.active || !NetworkClient.ready)
        {
            PlayAttackLocal(attackDir, currentWeaponIndex);
            WeaponDefinition weapon = GetCurrentWeapon();
            if (weapon != null)
                nextAttackTime = Time.time + Mathf.Max(weapon.attackCooldown, 0.01f);
            return;
        }

        CmdRequestAttack(attackDir);
    }

    private void TryEquipWeapon(int index)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;
        if (!NetworkClient.active || !NetworkClient.ready) return;

        CmdEquipWeapon(index);
    }

    [Command]
    private void CmdEquipWeapon(int index)
    {
        if (weapons == null || weapons.Length == 0) return;
        if (index < 0 || index >= weapons.Length) return;

        currentWeaponIndex = index;
    }

    [Command]
    private void CmdRequestAttack(Vector2 attackDir)
    {
        WeaponDefinition weapon = GetCurrentWeapon();
        if (weapon == null) return;

        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + Mathf.Max(weapon.attackCooldown, 0.01f);

        if (attackDir.sqrMagnitude <= 0.0001f)
            attackDir = Vector2.down;

        attackDir.Normalize();
        RpcPlayAttack(attackDir, currentWeaponIndex);
    }

    [ClientRpc]
    private void RpcPlayAttack(Vector2 attackDir, int weaponIndex)
    {
        // Local client đã phát đòn dự đoán thì bỏ qua replay để tránh giật animation.
        if (isLocalPlayer && isSwinging) return;

        PlayAttackLocal(attackDir, weaponIndex);
    }

    private void PlayAttackLocal(Vector2 attackDir, int weaponIndex)
    {
        if (weaponIndex >= 0 && weaponIndex < weapons.Length)
            ApplyWeaponVisual(weaponIndex);

        StopCoroutineSafe();
        StartCoroutine(SwingRoutine(attackDir));
    }

    private IEnumerator SwingRoutine(Vector2 attackDir)
    {
        WeaponDefinition weapon = GetCurrentWeapon();
        if (weapon == null) yield break;

        isSwinging = true;

        float duration = Mathf.Max(weapon.swingDuration, 0.03f);
        float baseAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg + weapon.idleAngleOffset;
        float halfArc = weapon.swingArc * 0.5f;

        Vector3 holdPos = attackDir.normalized * weapon.holdDistance;
        if (weaponPivot != null)
            weaponPivot.localPosition = holdPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float angle = Mathf.Lerp(baseAngle - halfArc, baseAngle + halfArc, t);

            if (weaponPivot != null)
                weaponPivot.localRotation = Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        isSwinging = false;
        UpdateIdleWeaponPose();
    }

    private void UpdateIdleWeaponPose()
    {
        if (isSwinging) return;

        WeaponDefinition weapon = GetCurrentWeapon();
        if (weapon == null || weaponPivot == null) return;

        Vector2 dir = GetFacingDirection();
        if (dir.sqrMagnitude <= 0.0001f) dir = Vector2.down;
        dir.Normalize();

        weaponPivot.localPosition = (Vector3)(dir * weapon.holdDistance);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + weapon.idleAngleOffset;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (playerRenderer != null)
            ApplyVisualSorting(dir);
    }

    private Vector2 GetFacingDirection()
    {
        if (movement != null && movement.LastMoveDirection.sqrMagnitude > 0.0001f)
            return movement.LastMoveDirection;

        if (networkState != null && networkState.LastMoveDirection.sqrMagnitude > 0.0001f)
            return networkState.LastMoveDirection;

        return Vector2.down;
    }

    private WeaponDefinition GetCurrentWeapon()
    {
        if (weapons == null || weapons.Length == 0) return null;
        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, weapons.Length - 1);
        return weapons[currentWeaponIndex];
    }

    private void OnWeaponIndexChanged(int oldIndex, int newIndex)
    {
        ApplyWeaponVisual(newIndex);
    }

    private void ApplyWeaponVisual(int weaponIndex)
    {
        EnsureWeaponObjects();

        if (weapons == null || weapons.Length == 0) return;
        if (weaponIndex < 0 || weaponIndex >= weapons.Length) return;

        WeaponDefinition weapon = weapons[weaponIndex];
        RebuildWeaponVisual(weapon);
        UpdateIdleWeaponPose();
    }

    private void RebuildWeaponVisual(WeaponDefinition weapon)
    {
        if (weaponPivot == null) return;

        if (spawnedWeaponVisual != null)
        {
            Destroy(spawnedWeaponVisual);
            spawnedWeaponVisual = null;
        }

        activeWeaponRenderers = null;

        if (weapon != null && weapon.weaponPrefab != null)
        {
            spawnedWeaponVisual = Instantiate(weapon.weaponPrefab, weaponPivot);
            spawnedWeaponVisual.name = weapon.weaponPrefab.name;
            spawnedWeaponVisual.transform.localPosition = Vector3.zero;
            spawnedWeaponVisual.transform.localRotation = Quaternion.identity;
            spawnedWeaponVisual.transform.localScale = Vector3.one;

            activeWeaponRenderers = spawnedWeaponVisual.GetComponentsInChildren<SpriteRenderer>(true);

            if (weaponRenderer != null)
                weaponRenderer.enabled = false;

            return;
        }

        if (weaponRenderer == null) return;

        weaponRenderer.sprite = weapon != null ? weapon.sprite : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
        activeWeaponRenderers = weaponRenderer.enabled ? new[] { weaponRenderer } : null;
    }

    private void ApplyVisualSorting(Vector2 facingDir)
    {
        if (playerRenderer == null || activeWeaponRenderers == null) return;

        int order = facingDir.y > 0 ? playerRenderer.sortingOrder - 1 : playerRenderer.sortingOrder + 1;
        int layerId = playerRenderer.sortingLayerID;

        for (int i = 0; i < activeWeaponRenderers.Length; i++)
        {
            SpriteRenderer sr = activeWeaponRenderers[i];
            if (sr == null) continue;
            sr.sortingLayerID = layerId;
            sr.sortingOrder = order;
        }
    }

    private void EnsureWeaponObjects()
    {
        if (weaponPivot == null)
        {
            Transform existing = transform.Find("WeaponPivot");
            if (existing != null)
            {
                weaponPivot = existing;
            }
            else
            {
                GameObject go = new GameObject("WeaponPivot");
                weaponPivot = go.transform;
                weaponPivot.SetParent(transform, false);
            }
        }

        if (weaponRenderer == null && weaponPivot != null)
        {
            Transform existing = weaponPivot.Find("WeaponVisual");
            if (existing != null)
            {
                weaponRenderer = existing.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject go = new GameObject("WeaponVisual");
                go.transform.SetParent(weaponPivot, false);
                weaponRenderer = go.AddComponent<SpriteRenderer>();
            }
        }
    }

    private void StopCoroutineSafe()
    {
        if (!isSwinging) return;
        StopAllCoroutines();
        isSwinging = false;
    }
}
