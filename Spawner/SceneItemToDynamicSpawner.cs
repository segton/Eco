using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SceneItemToDynamicSpawner : NetworkBehaviour
{
    // Assign this in the Inspector.
    // This prefab must have a NetworkObject component and be registered in your NetworkManager's network prefab list.
    [Tooltip("This prefab will be spawned dynamically. It must have a NetworkObject and NetworkTransform component, and be registered with your NetworkManager.")]
    public GameObject networkedItemPrefab;

    // This flag ensures we only run the spawner once on the server.
    private bool hasProcessed = false;

    public override void OnNetworkSpawn()
    {
        // We only want to run this on the server.
        if (!IsServer) return;
        if (hasProcessed) return;

        ProcessPlaceholdersAndSpawn();
    }

    private void ProcessPlaceholdersAndSpawn()
    {
        // 1. Find all placeholder items in the scene.
        //    IMPORTANT: These should be the design-time items placed in the scene (with the "Item" script) 
        //    that are meant only to provide a spawn location. They should NOT have a NetworkObject component.
        Item[] placeholders = FindObjectsOfType<Item>(includeInactive: true);

        // 2. For each placeholder, record its transform data.
        List<(Vector3 position, Quaternion rotation)> spawnData = new List<(Vector3, Quaternion)>();
        foreach (var placeholder in placeholders)
        {
            spawnData.Add((placeholder.transform.position, placeholder.transform.rotation));
        }

        // 3. Destroy the placeholders on the server (and because Destroy is a local call, this will run on the server only).
        //    To ensure clients also do not have ghost placeholders, make sure these objects are not present in your
        //    scene build (for example, use EditorOnly tag or remove them via a dedicated client script).
        foreach (var placeholder in placeholders)
        {
            Destroy(placeholder.gameObject);
        }

        // 4. Spawn the dynamic network objects at each recorded position.
        foreach (var (pos, rot) in spawnData)
        {
            if (networkedItemPrefab == null)
            {
                Debug.LogError("[DynamicItemSpawner] networkedItemPrefab is not assigned!");
                continue;
            }
            GameObject instance = Instantiate(networkedItemPrefab, pos, rot);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log($"[DynamicItemSpawner] Spawned networked item at {pos}");
            }
            else
            {
                Debug.LogError($"[DynamicItemSpawner] Prefab {networkedItemPrefab.name} is missing a NetworkObject component!");
            }
        }

        hasProcessed = true;
    }
}
