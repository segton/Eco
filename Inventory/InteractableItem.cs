/*using UnityEngine;
using Unity.Netcode;

public class InteractableItem : NetworkBehaviour
{
    public int itemID;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerHotbar hotbar))
        {
            Debug.Log($"Item {itemID} collided with {other.name}, attempting pickup.");
            RequestPickupServerRpc(hotbar.OwnerClientId, itemID);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickupServerRpc(ulong clientId, int itemID, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogError($"[ERROR] Player {clientId} is not a valid connected client!");
            return;
        }

        PlayerHotbar hotbar = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<PlayerHotbar>();

        if (hotbar != null)
        {
            Debug.Log($"[SERVER] Processing pickup for Player {clientId}, Item ID: {itemID}");
            hotbar.AddItemToHotbar(itemID);

            // Confirm if item was added to hotbar
            if (hotbar.hotbarItems.Contains(itemID))
            {
                Debug.Log($"[SUCCESS] Item {itemID} successfully added to Player {clientId}'s hotbar.");
                GetComponent<NetworkObject>().Despawn();
            }
            else
            {
                Debug.LogError($" [FAIL] Item {itemID} was NOT added to Player {clientId}'s hotbar!");
            }
        }
        else
        {
            Debug.LogError($"[ERROR] PlayerHotbar not found for Player {clientId}");
        }
    }

}
*/