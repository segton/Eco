using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ItemFallDespawn : MonoBehaviour
{
    [Tooltip("Y-position below which the item will be despawned.")]
    public float killY = -100f;

    NetworkObject netObj;
    bool isServer;

    // Cache a reference to your Item script (which must expose the same itemID used by SpawnManager)
    private Item itemComp;
    private string itemID;

    void Awake()
    {
        netObj = GetComponent<NetworkObject>();
        isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // Assume your Item.cs has a public string itemID field:
        itemComp = GetComponent<Item>();
        if (itemComp != null)
        {
            itemID = itemComp.itemID;
        }
    }

    void Update()
    {
        if (!isServer) return;

        if (transform.position.y < killY)
        {
            // Before despawning, notify SpawnManager if this is a capped item
            if (!string.IsNullOrEmpty(itemID)
                && SpawnManager.Instance != null
                && SpawnManager.Instance.IsCappedItem(itemID))
            {
                SpawnManager.Instance.NotifyDestroyed(itemID);  // decrement the serverside count :contentReference[oaicite:0]{index=0}
            }

            // Now actually despawn/destroy
            if (netObj.IsSpawned)
                netObj.Despawn(destroy: true);
            else
                Destroy(gameObject);
        }
    }
}
