using UnityEngine;
using Unity.Netcode;

public class HotbarManager : NetworkBehaviour
{
    private InventoryManager playerInventory;
    private HotbarUI hotbarUI;
    private Canvas hotbarCanvas;

    void Start()
    {
        // Keep HotbarManager enabled but manage visibility
        hotbarCanvas = GetComponentInParent<Canvas>();

        if (IsOwner)
        {
            ShowHotbarForLocalPlayer();
            FindLocalPlayerInventory();
        }
        else
        {
            HideHotbarForOtherPlayers();
        }
    }

    void ShowHotbarForLocalPlayer()
    {
        if (hotbarCanvas != null)
        {
            foreach (Transform child in hotbarCanvas.transform)
            {
                child.gameObject.SetActive(true); // Enable only the local player's UI elements
            }
            Debug.Log("Hotbar UI enabled for local player.");
        }
        else
        {
            Debug.LogError("Hotbar Canvas not found in Player Prefab!");
        }
    }

    void HideHotbarForOtherPlayers()
    {
        if (hotbarCanvas != null)
        {
            foreach (Transform child in hotbarCanvas.transform)
            {
                child.gameObject.SetActive(false); // Hide UI elements for other players
            }
            Debug.Log("Hotbar UI hidden for non-local players.");
        }
    }

    void FindLocalPlayerInventory()
    {
        InventoryManager[] players = Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.GetComponent<NetworkObject>().IsOwner)
            {
                playerInventory = player;
                player.SetHotbarUI(GetComponent<HotbarUI>());
                Debug.Log("HotbarManager: Linked to Local Player's Inventory!");
                return;
            }
        }
        Debug.LogError("HotbarManager: Local Player Inventory not found!");
    }
}
