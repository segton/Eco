/*using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;

public class ResyncManager : MonoBehaviour
{
    void OnEnable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected. Resynchronizing networked objects...");

        // Iterate over your networked objects. For instance, if all items are spawned and have a common tag or are in a list:
        foreach (NetworkObject netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            // Option 1: If you have a custom method on your NetworkObject (or a component on it)
            var resync = netObj.GetComponent<IResyncable>();
            if (resync != null)
            {
                resync.ForceResync(clientId);
            }
            // Option 2: If using NetworkVariables, you might force a full update:
            //netObj.ForceNetworkUpdate();
        }
    }
}
*/