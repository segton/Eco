using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkItemSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject itemPrefab; // Prefab of the item to spawn
    private List<GameObject> spawnedItems = new List<GameObject>();

    public static NetworkItemSpawner Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    [ServerRpc]
    public void SpawnItemServerRpc(Vector3 position, ulong clientId)
    {
        if (!IsServer) return;

        GameObject item = Instantiate(itemPrefab, position, Quaternion.identity);
        item.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedItems.Add(item);
    }
}
