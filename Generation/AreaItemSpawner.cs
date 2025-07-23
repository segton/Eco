using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class AreaItemSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("List of item prefabs (must include a NetworkObject on root).")]
    public List<GameObject> spawnPrefabs;

    [Tooltip("How many items this spawner will attempt to spawn at Start.")]
    public int minSpawnCount = 2;
    public int maxSpawnCount = 5;

    [Header("Global Outside Loot Cap")]
    [Tooltip("If true, never spawn more than `globalLootLimit` items total across all AreaItemSpawners.")]
    public bool enforceGlobalLootLimit = false;
    [Tooltip("Maximum total items to spawn across all outside-area spawners.")]
    public int globalLootLimit = 50;

    [Header("Per-Spawner Cap")]
    [Tooltip("Hard cap on how many items this single spawner can emit.")]
    public int maxPerSpawner = 20;

    // Tracks across all instances
    private static int s_TotalGlobalSpawned = 0;

    private BoxCollider _box;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;             // only spawn on host/ server
        _box = GetComponent<BoxCollider>();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        int toSpawn = Random.Range(minSpawnCount, maxSpawnCount + 1);
        int spawned = 0;

        for (int i = 0; i < toSpawn; i++)
        {
            // global cap reached?
            if (enforceGlobalLootLimit && s_TotalGlobalSpawned >= globalLootLimit)
                yield break;

            // per-spawner cap reached?
            if (spawned >= maxPerSpawner)
                yield break;

            // pick a random prefab
            var prefab = spawnPrefabs[Random.Range(0, spawnPrefabs.Count)];

            // calculate a random point inside the BoxCollider volume
            Vector3 localCenter = _box.center;
            Vector3 halfExtents = _box.size * 0.5f;
            Vector3 randomOffset = new Vector3(
                Random.Range(-halfExtents.x, halfExtents.x),
                Random.Range(-halfExtents.y, halfExtents.y),
                Random.Range(-halfExtents.z, halfExtents.z)
            );
            Vector3 worldPos = transform.TransformPoint(localCenter + randomOffset);

            // instantiate & spawn
            var go = Instantiate(prefab, worldPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn();
            else
                Debug.LogWarning($"[{nameof(AreaItemSpawner)}] Prefab {prefab.name} is missing a NetworkObject!");

            spawned++;
            s_TotalGlobalSpawned++;
            yield return null;  // spread out work over frames
        }
    }
}
