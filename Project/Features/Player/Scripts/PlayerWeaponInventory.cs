using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerWeaponInventory : NetworkBehaviour
{
    public class SyncWeapons : SyncList<WeaponInstanceData>
    {
    }

    [SerializeField] private int maxWeapons = 60;

    public event Action OnWeaponsChanged;

    private readonly SyncWeapons weapons = new SyncWeapons();
    private readonly List<WeaponInstanceData> snapshot = new List<WeaponInstanceData>();

    public IReadOnlyList<WeaponInstanceData> Weapons => snapshot;

    public override void OnStartClient()
    {
        base.OnStartClient();
        weapons.OnChange += OnWeaponsSyncChanged;
        RebuildSnapshot();
    }

    public override void OnStopClient()
    {
        weapons.OnChange -= OnWeaponsSyncChanged;
        base.OnStopClient();
    }

    [Server]
    public bool TryAddWeaponServer(WeaponInstanceData weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon.instanceId))
        {
            return false;
        }

        if (weapons.Count >= Mathf.Max(1, maxWeapons))
        {
            return false;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].instanceId == weapon.instanceId)
            {
                return false;
            }
        }

        weapons.Add(weapon);
        return true;
    }

    [Server]
    public bool TryRemoveWeaponServer(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].instanceId != instanceId) continue;
            weapons.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void OnWeaponsSyncChanged(SyncWeapons.Operation op, int index, WeaponInstanceData item)
    {
        RebuildSnapshot();
    }

    private void RebuildSnapshot()
    {
        snapshot.Clear();
        for (int i = 0; i < weapons.Count; i++)
        {
            snapshot.Add(weapons[i]);
        }

        if (isLocalPlayer)
        {
            OnWeaponsChanged?.Invoke();
        }
    }
}
