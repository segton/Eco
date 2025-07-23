using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public class SpawnInfo
{
    public GameObject itemPrefab; // Must have a NetworkObject
    public Vector3 spawnPosition;
    public Quaternion spawnRotation = Quaternion.identity;
}

public class ItemSpawner : NetworkBehaviour
{
    public List<SpawnInfo> spawnInfos = new List<SpawnInfo>();
    public float spawnDelay = 1.0f;

    void Start()
    {
        if (!IsServer) return;
        StartCoroutine(SpawnItemsCoroutine());
    }

    private IEnumerator SpawnItemsCoroutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        foreach (var spawn in spawnInfos)
        {
            if (spawn.itemPrefab != null)
            {
                GameObject instance = Instantiate(spawn.itemPrefab, spawn.spawnPosition, spawn.spawnRotation);
                NetworkObject netObj = instance.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    Debug.Log($"[ItemSpawner] Spawned {spawn.itemPrefab.name} at {spawn.spawnPosition}");
                }
                else
                {
                    Debug.LogError($"[ItemSpawner] {spawn.itemPrefab.name} is missing a NetworkObject component!");
                }
            }
        }
    }
}
