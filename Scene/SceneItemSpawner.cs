using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class SceneItemSpawner : NetworkBehaviour
{
    [Tooltip("The fallback networked prefab if an itemID lookup fails.")]
    public GameObject fallbackNetworkedPrefab;

    [Tooltip("Delay (in seconds) before processing placeholders.")]
    public float spawnDelay = 1f;

    bool hasProcessed = false;

    struct SpawnRecord
    {
        public int prefabIndex;
        public Vector3 position;
        public Quaternion rotation;
    }
    List<SpawnRecord> spawnRecords = new List<SpawnRecord>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // only the host processes placeholders
            if (!hasProcessed)
                StartCoroutine(ProcessPlaceholders());
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    IEnumerator ProcessPlaceholders()
    {
        yield return new WaitForSeconds(spawnDelay);

        var placeholders = FindObjectsOfType<ScenePlaceholder>(includeInactive: true);
        if (placeholders.Length == 0)
        {
            Debug.LogWarning("[SceneItemSpawner] no placeholders found");
            yield break;
        }

        // 1) record all placeholder data
        foreach (var ph in placeholders)
        {
            var entry = ItemDatabase.Instance.GetItem(ph.itemID);
            GameObject prefab = (entry != null && entry.prefab != null)
                                ? entry.prefab
                                : fallbackNetworkedPrefab;

            if (prefab == null)
            {
                Debug.LogError($"[SceneItemSpawner] no prefab for ID {ph.itemID}");
                continue;
            }

            Vector3 pos = ph.transform.position;
            Quaternion rot = ph.transform.rotation;

            // instantiate on host
            var inst = Instantiate(prefab, pos, rot);
            var net = inst.GetComponent<NetworkObject>();
            if (net == null)
            {
                Debug.LogError($"[SceneItemSpawner] prefab {prefab.name} missing NetworkObject");
                Destroy(inst);
                continue;
            }
            net.Spawn(); // dynamic spawn to existing clients

            // record for late joiners
            int idx = Array.IndexOf(ItemDatabase.Instance.items, entry);
            spawnRecords.Add(new SpawnRecord
            {
                prefabIndex = idx,
                position = pos,
                rotation = rot
            });
        }

        // 2) remove all placeholder objects on host
        foreach (var ph in placeholders)
            Destroy(ph.gameObject);

        hasProcessed = true;

        // 3) tell existing clients to clean up their placeholders
        RemovePlaceholdersClientRpc();
    }

    [ClientRpc]
    void RemovePlaceholdersClientRpc()
    {
        if (IsServer) return;

        foreach (var ph in FindObjectsOfType<ScenePlaceholder>(includeInactive: true))
        {
            // if a network object with same transform already exists, skip
            var net = ph.GetComponent<NetworkObject>();
            if (net != null && net.IsSpawned)
                continue;
            Destroy(ph.gameObject);
        }
    }

    void OnClientConnected(ulong clientId)
    {
        // replay each recorded spawn to just that one client
        foreach (var rec in spawnRecords)
        {
            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            };
            ReplaySpawnClientRpc(rec.prefabIndex, rec.position, rec.rotation, rpcParams);
        }
    }

    [ClientRpc]
    void ReplaySpawnClientRpc(int prefabIndex, Vector3 pos, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (IsServer) return;
        var entry = ItemDatabase.Instance.items[prefabIndex];
        Instantiate(entry.prefab, pos, rot);
    }
}
