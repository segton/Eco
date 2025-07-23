/*using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class BaseItemSpawner : NetworkBehaviour
{
    [SerializeField] protected GameObject itemPrefab;
    protected List<GameObject> spawnedItems = new List<GameObject>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && IsServer)
        {
            ulong clientId = other.GetComponent<NetworkObject>().OwnerClientId;
            SpawnItemServerRpc(transform.position, clientId);
        }
    }

    [ServerRpc]
    public virtual void SpawnItemServerRpc(Vector3 position, ulong clientId)
    {
        if (!IsServer) return;

        GameObject item = Instantiate(itemPrefab, position, Quaternion.identity);
        item.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedItems.Add(item);
    }
}

public class WeaponSpawner : BaseItemSpawner
{
    [ServerRpc]
    public override void SpawnItemServerRpc(Vector3 position, ulong clientId)
    {
        if (!IsServer) return;

        GameObject weapon = Instantiate(itemPrefab, position, Quaternion.identity);
        weapon.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedItems.Add(weapon);
    }
}

public class ToolSpawner : BaseItemSpawner
{
    [ServerRpc]
    public override void SpawnItemServerRpc(Vector3 position, ulong clientId)
    {
        if (!IsServer) return;

        GameObject tool = Instantiate(itemPrefab, position, Quaternion.identity);
        tool.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedItems.Add(tool);
    }
}

public class ConsumableSpawner : BaseItemSpawner
{
    [ServerRpc]
    public override void SpawnItemServerRpc(Vector3 position, ulong clientId)
    {
        if (!IsServer) return;

        GameObject consumable = Instantiate(itemPrefab, position, Quaternion.identity);
        consumable.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedItems.Add(consumable);
    }
}
*/