using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this at the top if using TextMeshPro


public class HotbarUI : MonoBehaviour
{
    public List<Image> slotImages = new List<Image>();
    public List<Image> highlightBorders = new List<Image>();
    public List<TextMeshProUGUI> slotTexts = new List<TextMeshProUGUI>();
    private InventoryManager playerInventory;
    private int selectedSlot = 0;

    public Color selectedColor = Color.yellow;
    public Color defaultColor = Color.white;

    void Start()
    {
        FindLocalPlayerInventory();
        UpdateSelection();
    }

    void Update()
    {
        HandleHotbarSelection();
    }

    void FindLocalPlayerInventory()
    {
        InventoryManager[] players = Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.GetComponent<NetworkObject>().IsOwner)
            {
                playerInventory = player;
                playerInventory.SetHotbarUI(this);
                return;
            }
        }
        Debug.LogError("HotbarUI: Local Player Inventory not found!");
    }

    private void HandleHotbarSelection()
    {
        for (int i = 0; i < slotImages.Count; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedSlot = i;
                playerInventory.RequestHotbarSlotChange(i); // Client asks the server to update
                UpdateSelection();
                return;
            }
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput > 0f)
        {
            selectedSlot = (selectedSlot - 1 + slotImages.Count) % slotImages.Count;
            playerInventory.RequestHotbarSlotChange(selectedSlot);
            UpdateSelection();
        }
        else if (scrollInput < 0f)
        {
            selectedSlot = (selectedSlot + 1) % slotImages.Count;
            playerInventory.RequestHotbarSlotChange(selectedSlot);
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        if (highlightBorders == null || highlightBorders.Count == 0)
        {
            Debug.LogWarning("Highlight Borders list is empty!");
            return;
        }

        for (int i = 0; i < slotImages.Count; i++)
        {
            if (i < highlightBorders.Count && highlightBorders[i] != null)
            {
                highlightBorders[i].enabled = (i == selectedSlot);
            }
        }
    }

    public void UpdateUI(NetworkList<ItemData> inventory)
    {
        for (int i = 0; i < slotImages.Count; i++)
        {
            if (i < inventory.Count && !inventory[i].IsEmpty)
            {
                ItemDatabase.ItemEntry entry = ItemDatabase.Instance.GetItem(inventory[i].itemID.ToString());
                slotImages[i].sprite = entry?.icon;
                slotImages[i].enabled = entry != null;
                if (i < slotTexts.Count && slotTexts[i] != null)
                    slotTexts[i].text = entry != null ? entry.itemName : "";
            }
            else
            {
                slotImages[i].sprite = null;
                slotImages[i].enabled = false;
                if (i < slotTexts.Count && slotTexts[i] != null)
                    slotTexts[i].text = "";
            }
        }
        UpdateSelection();
    }

}
