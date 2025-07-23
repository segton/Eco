using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // Store player inventory state keyed by client ID.
    private Dictionary<ulong, List<ItemData>> playerInventories = new Dictionary<ulong, List<ItemData>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Call this on the server when a player disconnects
    public void SavePlayerInventory(ulong clientId, List<ItemData> inventory)
    {
        if (playerInventories.ContainsKey(clientId))
        {
            playerInventories[clientId] = new List<ItemData>(inventory);
        }
        else
        {
            playerInventories.Add(clientId, new List<ItemData>(inventory));
        }
        Debug.Log($"[GameStateManager] Saved inventory for client {clientId}");
    }

    // Call this on the server (or new InventoryManager) when a player rejoins.
    public List<ItemData> GetPlayerInventory(ulong clientId)
    {
        if (playerInventories.TryGetValue(clientId, out List<ItemData> inventory))
        {
            return inventory;
        }
        return null;
    }

    // Optionally, clear saved data after restoring.
    public void ClearPlayerInventory(ulong clientId)
    {
        if (playerInventories.ContainsKey(clientId))
        {
            playerInventories.Remove(clientId);
        }
    }
}
