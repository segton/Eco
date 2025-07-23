using UnityEngine;
using Unity.Netcode;

public class SpawnedItemTracker : NetworkBehaviour
{
    [HideInInspector] public string itemID;

    /// <summary>
    /// This runs whenever the NetworkObject is despawned (server-side).
    /// </summary>
    public override void OnNetworkDespawn()
    {
        // Only the server should adjust counts
        if (IsServer && SpawnManager.Instance != null && !string.IsNullOrEmpty(itemID))
        {
            SpawnManager.Instance.NotifyDestroyed(itemID);
            Debug.Log($"[SpawnedItemTracker] OnNetworkDespawn  NotifyDestroyed('{itemID}')");
        }
        base.OnNetworkDespawn();
    }
}
