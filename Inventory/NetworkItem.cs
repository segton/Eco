/*using Unity.Netcode;
using UnityEngine;

public class NetworkItem : NetworkBehaviour
{
    public string itemName;
    public int itemID;
    private bool isPickedUp = false;

    public void Initialize(string name, int id)
    {
        itemName = name;
        itemID = id;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || isPickedUp) return;

        if (other.CompareTag("Player"))
        {
            PlayerHotbar playerHotbar = other.GetComponent<PlayerHotbar>();
            if (playerHotbar != null)
            {
                isPickedUp = true;
                Debug.Log($"[NetworkItem] {other.name} picked up {itemName}");
                PickupItemServerRpc(other.GetComponent<NetworkObject>().OwnerClientId);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickupItemServerRpc(ulong clientId)
    {
        PlayerHotbar playerHotbar = FindPlayerByClientId(clientId);
        if (playerHotbar == null)
        {
            Debug.LogError($"[NetworkItem] PlayerHotbar not found for client {clientId}");
            return;
        }

        //Find the next available hotbar slot
        int slot = playerHotbar.GetNextAvailableSlot();
        if (slot == -1)
        {
            Debug.LogError($"[NetworkItem] No available hotbar slots for player {clientId}!");
            return;
        }

        //Ensure the item has a valid icon name
        string itemIconName = itemName; // Assuming itemName is the name used in Resources/Sprites/
        Debug.Log($"[NetworkItem] Player {clientId} successfully picked up {itemName} in slot {slot} with icon {itemIconName}");

        playerHotbar.AddItemToHotbar(gameObject, slot, itemIconName);

        //Ensure the item is despawned by the server
        DespawnItemServerRpc();
    }



    [ServerRpc(RequireOwnership = false)]
    private void DespawnItemServerRpc()
    {
        Debug.Log("[NetworkItem] Despawning item on server");
        GetComponent<NetworkObject>().Despawn();
        gameObject.SetActive(false);
    }

    private PlayerHotbar FindPlayerByClientId(ulong clientId)
    {
        foreach (var player in FindObjectsByType<PlayerHotbar>(FindObjectsSortMode.None))
        {
            if (player.OwnerClientId == clientId)
            {
                return player;
            }
        }
        return null;
    }
}
*/