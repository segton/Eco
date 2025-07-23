/*using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class PlayerHotbar : NetworkBehaviour
{
    private const int HOTBAR_SIZE = 5;
    private NetworkVariable<int> selectedSlot = new NetworkVariable<int>(0);
    public GameObject[] hotbar = new GameObject[HOTBAR_SIZE];
    [SerializeField] private HotbarUI hotbarUI;

    private void Start()
    {
        if (IsOwner)
        {
            hotbarUI.Initialize(HOTBAR_SIZE);
        }
    }

    public void AddItemToHotbar(GameObject item, int slot, string itemIconName)
    {
        if (slot < 0 || slot >= HOTBAR_SIZE)
        {
            Debug.LogError($"[PlayerHotbar] Slot {slot} is out of bounds!");
            return;
        }

        if (item == null)
        {
            Debug.LogError("[PlayerHotbar] Item is NULL when adding to hotbar!");
            return;
        }

        hotbar[slot] = item;
        Debug.Log($"[PlayerHotbar] Added {item.name} to slot {slot}");

        //  Ensure the hotbar UI updates correctly
        UpdateHotbarUIClientRpc(slot, item.name, itemIconName);
    }




    public int GetNextAvailableSlot()
    {
        for (int i = 0; i < HOTBAR_SIZE; i++)
        {
            if (hotbar[i] == null)
            {
                return i;
            }
        }
        return -1; // No available slot
    }


    [ServerRpc]
    public void SwitchHotbarSlotServerRpc(int newSlot)
    {
        if (newSlot < 0 || newSlot >= HOTBAR_SIZE) return;
        selectedSlot.Value = newSlot;
        UpdateSelectedSlotClientRpc(newSlot);
    }

    [ClientRpc]
    private void UpdateHotbarUIClientRpc(int slot, string itemName, string itemIconName)
    {
        if (IsOwner)
        {
            Debug.Log($"[HotbarUI] Updating slot {slot} with {itemName}");
            hotbarUI.UpdateSlot(slot, itemName, itemIconName);
        }
    }


    [ClientRpc]
    private void UpdateSelectedSlotClientRpc(int newSlot)
    {
        if (IsOwner)
        {
            hotbarUI.HighlightSlot(newSlot);
        }
    }
}*/
