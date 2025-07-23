using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Serializable]
    public struct SpawnOption
    {
        public string itemID;     // must match ItemDatabase
        public Transform spawnPoint;
    }

    [Header("Which items can be spawned & where")]
    [SerializeField] private SpawnOption[] spawnOptions;

    // SERVER ONLY: current counts by itemID
    private readonly Dictionary<string, int> _spawnCounts = new();

    private void Awake()
    {
        // remove the IsServer guard here so clients also get a reference
        Instance = this;
    }

    /// <summary>
    /// Call this from your UI Button OnClick (pass in the index).
    /// </summary>
    public void SpawnByIndex(int idx)
    {
        // no IsOwner check here—ANY client can request a spawn
        if (idx < 0 || idx >= spawnOptions.Length)
            return;

        RequestSpawnServerRpc(idx);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(int idx, ServerRpcParams rpcParams = default)
    {
        // server does all the validation & spawning
        var def = spawnOptions[idx];
        if (string.IsNullOrEmpty(def.itemID) || def.spawnPoint == null)
            return;

        var entry = ItemDatabase.Instance?.GetItem(def.itemID);
        if (entry == null || entry.prefab == null)
            return;

        int cap = NetworkManager.Singleton.ConnectedClientsList.Count;
        _spawnCounts.TryGetValue(def.itemID, out int current);
        if (current >= cap)
            return;

        var go = Instantiate(entry.prefab, def.spawnPoint.position, def.spawnPoint.rotation);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"SpawnManager: prefab {entry.prefab.name} missing NetworkObject!");
            Destroy(go);
            return;
        }

        // track this new instance
        NotifySpawned(def.itemID);

        netObj.Spawn();
        _spawnCounts[def.itemID] = current + 1;
        Debug.Log($"Spawned {def.itemID} ({current + 1}/{cap})");
    }

    public bool IsCappedItem(string itemID)
    {
        foreach (var opt in spawnOptions)
            if (opt.itemID == itemID)
                return true;
        return false;
    }

    internal void NotifySpawned(string itemID)
    {
        if (!IsServer) return;
        _spawnCounts.TryGetValue(itemID, out int cnt);
        _spawnCounts[itemID] = cnt + 1;
        Debug.Log($"[SpawnManager] NotifySpawned('{itemID}')  {_spawnCounts[itemID]}");
    }

    internal void NotifyDestroyed(string itemID)
    {
        if (!IsServer) return;
        if (!_spawnCounts.TryGetValue(itemID, out int cnt)) return;
        _spawnCounts[itemID] = Mathf.Max(0, cnt - 1);
        Debug.Log($"[SpawnManager] NotifyDestroyed('{itemID}')  {_spawnCounts[itemID]}");
    }
}
