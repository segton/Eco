using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.EventSystems.EventTrigger;


public class InventoryManager : NetworkBehaviour
{
    private bool _prevVHeld = false;

    public static InventoryManager LocalInstance { get; private set; }
    public int hotbarSize = 5;
    public NetworkList<ItemData> inventory = new NetworkList<ItemData>();

    private HotbarUI hotbarUI;
    public NetworkVariable<int> selectedSlot = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private HashSet<ulong> despawnedItems = new HashSet<ulong>();
    public GameObject handItemParent;
    public GameObject currentHeldItem;
    public Camera cam;
    public GameObject heldItemPlaceholder;
    private bool _suppressInventoryChangeEvent = false;
    public float throwChargeTime = 0f;
    public float maxThrowTime = 2f; // Maximum time to hold Q for full throw
    public bool isChargingThrow = false;
    // remember who we were talking on
    private WalkieTalkie _lastHeldWalkie;

    private void Awake()
    {
        if (IsOwner) // Only the local player sets this.
        {
            LocalInstance = this;
        }
        DontDestroyOnLoad(gameObject);
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            inventory.Clear();
            for (int i = 0; i < hotbarSize; i++)
            {
                inventory.Add(new ItemData());
            }
        }

        inventory.OnListChanged += HandleInventoryChanged;
        selectedSlot.OnValueChanged += (oldValue, newValue) =>
        {
            UpdateHeldItem();
        };

        if (IsClient)
        {
            StartCoroutine(WaitForInitialSync());
        }

        // When a new client joins, sync the state so that removed items aren’t still present.
        if (IsServer)
        {
            StartCoroutine(SyncInventoryWithNewClients());
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void UpdateBatteryLevelServerRpc(int slotIndex, float newLevel)
    {
        if (slotIndex < inventory.Count && !inventory[slotIndex].IsEmpty)
        {
            ItemData data = inventory[slotIndex];
            data.batteryLevel = newLevel;
            inventory[slotIndex] = data;
            Debug.Log($"[InventoryManager] Updated battery level for slot {slotIndex} to {newLevel}");

            // Also update BatteryManager on the server.
            if (BatteryManager.Instance != null)
            {
                BatteryManager.Instance.SaveBatteryLevel(data.uniqueItemID.ToString(), newLevel);
            }
        }
    }



    // This RPC tells clients to remove any world items that have been picked up.
    [ClientRpc]
    private void RemoveDespawnedItemsClientRpc(DespawnedItemsList removedItemIds)
    {
        foreach (ulong id in removedItemIds.itemIds)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
            {
                Destroy(netObj.gameObject); // Remove unwanted world items
            }
        }
    }

    private IEnumerator SyncInventoryWithNewClients()
    {
        yield return new WaitForSeconds(1f); // Wait a bit for all clients to sync
        List<ulong> removedItems = new List<ulong>(despawnedItems);
        RemoveDespawnedItemsClientRpc(new DespawnedItemsList(removedItems));
    }

    private IEnumerator WaitForInitialSync()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("Ensuring initial hand sync...");
        UpdateHeldItem();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // Drop every item (or clear it) when the player disconnects.
            for (int i = 0; i < inventory.Count; i++)
            {
                if (!inventory[i].IsEmpty)
                {
                    DropItemServerRpc(i);
                }
            }
        }
        base.OnNetworkDespawn();
    }

    [ClientRpc]
    private void DespawnItemClientRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            Destroy(netObj.gameObject);
            Debug.Log("item destroyed");
        }
    }
    // InventoryManager.cs (inside your existing class)

    /// <summary>
    /// Called by the player on death to drop every slot server-side.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void DropAllItemsServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        // use a for-loop so we don’t mutate while iterating
        for (int i = 0; i < inventory.Count; i++)
        {
            if (!inventory[i].IsEmpty)
                DropItemServerRpc(i);
        }
    }

    void Start()
    {
        if (IsOwner)
        {
            StartCoroutine(AssignHotbarUI());
        }
    }

    private void HandleInventoryChanged(NetworkListEvent<ItemData> changeEvent)
    {
        // If we're intentionally suppressing inventory change events, do nothing.
        if (_suppressInventoryChangeEvent)
            return;

        // Update the hotbar UI for the local owner.
        if (IsOwner && hotbarUI != null)
        {
            hotbarUI.UpdateUI(inventory);
        }

        // Check if the changed item is in the currently selected slot.
        if (changeEvent.Index == selectedSlot.Value)
        {
            // Compare the previous and new values.
            // If the itemID hasn't changed, but the batteryLevel did,
            // assume it's just a battery update.
            if (changeEvent.PreviousValue.itemID.Equals(changeEvent.Value.itemID) &&
                Mathf.Abs(changeEvent.Value.batteryLevel - changeEvent.PreviousValue.batteryLevel) > 0.1f)
            {
                if (currentHeldItem != null)
                {
                    BatteryItem battery = currentHeldItem.GetComponentInChildren<BatteryItem>();
                    if (battery != null)
                    {
                        battery.currentBatteryLevel = changeEvent.Value.batteryLevel;
                        battery.ConfigureSlider();
                        // Exit early to avoid re-instantiating the held item.
                        return;
                    }
                }
            }
        }

        // For other types of changes (or if currentHeldItem is null), re-instantiate the held item.
        UpdateHeldItem();
    }

    private IEnumerator AssignHotbarUI()
    {
        yield return new WaitForSeconds(0.5f);
        hotbarUI = GetComponentInChildren<HotbarUI>();
        if (hotbarUI == null)
        {
            Debug.LogError("HotbarUI not found in player prefab!");
        }
    }

    public void SetHotbarUI(HotbarUI hotbar)
    {
        hotbarUI = hotbar;
    }

    public void RequestHotbarSlotChange(int slotIndex)
    {
        if (!IsOwner) return;
        RequestHotbarSlotChangeServerRpc(slotIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHotbarSlotChangeServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        SaveCurrentHeldBatteryLevel();
        selectedSlot.Value = slotIndex;
        UpdateHeldItemClientRpc(slotIndex);
    }

    [ClientRpc]
    private void UpdateHeldItemClientRpc(int slotIndex)
    {
        UpdateHeldItem();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Pickup and drop controls.
        if (Input.GetKeyDown(KeyCode.E)) TryPickupItem();
        if (Input.GetKeyDown(KeyCode.Q))
        {
            isChargingThrow = true;
            throwChargeTime = 0f;
        }

        // Charging duration
        if (isChargingThrow && Input.GetKey(KeyCode.Q))
        {
            throwChargeTime += Time.deltaTime;
        }

        // Release throw
        if (Input.GetKeyUp(KeyCode.Q))
        {
            isChargingThrow = false;
            float normalizedForce = Mathf.Clamp01(throwChargeTime / maxThrowTime);
            DropItemWithForceRequest(normalizedForce);
        }


        // Update Vivox channel volume based on whether the inventory contains a walkie.
        bool hasWalkie = CheckIfInventoryContainsWalkie();
        int desiredVolume = hasWalkie ? 0 : -80;
        try
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn &&
                VivoxService.Instance.ActiveChannels.ContainsKey(VoiceChannelManager.walkieChannelName))
            {
                _ = VivoxService.Instance.SetChannelVolumeAsync(VoiceChannelManager.walkieChannelName, desiredVolume);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InventoryManager] Failed to update channel volume: {ex.Message}");
        }
        WalkieTalkie stillHeld = IsHoldingWalkie()
           ? currentHeldItem.GetComponent<WalkieTalkie>()
           : null;

        if (_lastHeldWalkie != null && stillHeld != _lastHeldWalkie)
        {
            _lastHeldWalkie.StopTalk();
            var batt = _lastHeldWalkie.GetComponentInChildren<BatteryItem>();
            if (batt != null) batt.inUse(false);
            _lastHeldWalkie = null;
        }
        /*
        // Check for walkie talkie key events.
        if (IsHoldingWalkie())
        {
            var wt = currentHeldItem.GetComponent<WalkieTalkie>();

            bool vDown = Input.GetKeyDown(KeyCode.V);
            bool vUp = Input.GetKeyUp(KeyCode.V);

            if (vDown && !wt.IsTransmitting)
                wt.StartTalk();

            if (vUp && wt.IsTransmitting)
                wt.StopTalk();

            _lastHeldWalkie = wt;
        }
        if (_lastHeldWalkie != null && _lastHeldWalkie.IsTransmitting && !Input.GetKey(KeyCode.V))
        {
            _lastHeldWalkie.StopTalk();
            var batt = _lastHeldWalkie.GetComponentInChildren<BatteryItem>();
            if (batt != null) batt.inUse(false);
            _lastHeldWalkie = null;
        }*/
        bool vHeld = Input.GetKey(KeyCode.V);

        if (IsHoldingWalkie())
        {
            var wt = currentHeldItem.GetComponent<WalkieTalkie>();

            // pressed this frame?
            if (vHeld && !_prevVHeld)
            {
                // try to start transmitting
                StartCoroutine(StartTalkWithRetry(wt));
                _lastHeldWalkie = wt;
            }
            // released this frame?
            else if (!vHeld && _prevVHeld)
            {
                wt.StopTalk();
            }
        }
        else
        {
            // If we lost the walkie midtransmit, shut it down
            if (_lastHeldWalkie != null && _lastHeldWalkie.IsTransmitting)
                _lastHeldWalkie.StopTalk();
            _lastHeldWalkie = null;
        }

        _prevVHeld = vHeld;
    }
    private IEnumerator StartTalkWithRetry(WalkieTalkie wt)
    {
        const float retryInterval = 0.1f;
        const float maxRetryTime = 1f;
        float elapsed = 0f;

        wt.StartTalk();

        // as long as the key is still down, keep retrying until Vivox flips IsTransmitting
        while (!wt.IsTransmitting && elapsed < maxRetryTime && Input.GetKey(KeyCode.V))
        {
            yield return new WaitForSeconds(retryInterval);
            elapsed += retryInterval;
            wt.StartTalk();
        }

        if (!wt.IsTransmitting)
            Debug.LogWarning("[Walkie] Failed to start transmitting within timeout.");
    }

    public bool CheckIfInventoryContainsWalkie()
    {
        // First, check the currently held item.
        if (currentHeldItem != null)
        {
            WalkieTalkie wt = currentHeldItem.GetComponent<WalkieTalkie>();
            if (wt != null)
            {
                BatteryItem battery = currentHeldItem.GetComponentInChildren<BatteryItem>();
                if (battery != null)
                {
                    // Only consider it available if battery is above the empty threshold.
                    if (battery.currentBatteryLevel > 2f)
                        return true;
                    else
                        return false;
                }
            }
        }

        // Fallback: check the inventory records.
        for (int i = 0; i < inventory.Count; i++)
        {
            if (!inventory[i].IsEmpty)
            {
                var entry = ItemDatabase.Instance.GetItem(inventory[i].itemID.ToString());
                if (entry != null && entry.itemName == "Walkie")
                {
                    // Only consider it available if the stored battery level is above 2.
                    if (inventory[i].batteryLevel > 2f)
                        return true;
                }
            }
        }
        return false;
    }



    // Returns true if the current held item is a walkie talkie.
    public bool IsHoldingWalkie()
    {
        return currentHeldItem != null && currentHeldItem.GetComponent<WalkieTalkie>() != null;
    }

    private void TryPickupItem()
    {
        float pickupRange = 3f;
        int mask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, pickupRange, mask))
        {
            if (hit.collider.TryGetComponent<Item>(out var item))
            {
                // *** Set the last owner ID on the item here ***
                item.lastOwnerId = NetworkManager.Singleton.LocalClientId;
                Debug.Log($"[InventoryManager] Setting lastOwnerId for item {item.itemID} to {item.lastOwnerId}");

                NetworkObject netObj = item.GetComponent<NetworkObject>();

                // If the item is not spawned, assume it's already held.
                if (netObj == null || !netObj.IsSpawned)
                {
                    InventoryManager holder = item.GetComponentInParent<InventoryManager>();
                    if (holder != null)
                    {
                        StealItemServerRpc(holder.OwnerClientId, holder.selectedSlot.Value);
                    }
                    return;
                }

                // Proceed normally for world items.
                PickUpItemServerRpc(item.itemID, OwnerClientId, netObj);
            }
        }
    }


    [ServerRpc(RequireOwnership = false)]
    private void StealItemServerRpc(ulong victimClientId, int victimSlot, ServerRpcParams rpcParams = default)
    {
        ulong stealerId = rpcParams.Receive.SenderClientId;
        InventoryManager stealerInventory = FindPlayerInventoryById(stealerId);
        if (stealerInventory == null)
        {
            Debug.LogError($"[StealItem] Stealer inventory not found for {stealerId}");
            return;
        }

        int stealerSlot = stealerInventory.selectedSlot.Value;
        if (stealerSlot < 0 || stealerSlot >= stealerInventory.hotbarSize ||
            !stealerInventory.inventory[stealerSlot].IsEmpty)
        {
            Debug.LogWarning($"[StealItem] Player {stealerId} cannot steal: their slot {stealerSlot} is already occupied.");
            return;
        }

        InventoryManager victimInventory = FindPlayerInventoryById(victimClientId);
        if (victimInventory == null)
        {
            Debug.LogError($"[StealItem] Victim inventory not found for {victimClientId}");
            return;
        }

        if (victimSlot < 0 || victimSlot >= victimInventory.hotbarSize ||
            victimInventory.inventory[victimSlot].IsEmpty)
        {
            Debug.Log($"[StealItem] Victim's slot {victimSlot} is empty. Nothing to steal.");
            return;
        }

        ItemData stolenData = victimInventory.inventory[victimSlot];
        victimInventory.inventory[victimSlot] = new ItemData();

        stolenData.ownerId = stealerId;

        stealerInventory.inventory[stealerSlot] = stolenData;

        victimInventory.ForceUpdateHotbarClientRpc();
        victimInventory.ForceUpdateHandClientRpc(victimSlot);
        stealerInventory.ForceUpdateHotbarClientRpc();
        stealerInventory.ForceUpdateHandClientRpc(stealerSlot);

        Debug.Log($"[StealItem] Player {stealerId} stole slot {victimSlot} from {victimClientId} into their slot {stealerSlot}.");
    }




    [ServerRpc(RequireOwnership = false)]
    private void PickUpItemServerRpc(string itemID, ulong playerId, NetworkObjectReference itemObjectReference)
    {
        // Find the inventory for the player who is picking up the item.
        InventoryManager playerInventory = FindPlayerInventoryById(playerId);
        if (playerInventory == null)
        {
            Debug.LogError($"Inventory not found for player {playerId}");
            return;
        }

        ItemDatabase.ItemEntry itemEntry = ItemDatabase.Instance.GetItem(itemID);
        if (itemEntry == null)
        {
            Debug.LogError($"Item {itemID} not found in database!");
            return;
        }

        int slotIndex = playerInventory.selectedSlot.Value;
        if (!playerInventory.inventory[slotIndex].IsEmpty)
        {
            Debug.Log("Selected slot is occupied. Cannot pick up item.");
            return;
        }

        // Retrieve dynamic data from the dropped item.
        string existingUniqueID = "";
        float existingBattery = 100f;
        ulong droppedItemOwnerId = 0; // Will hold the last owner id from the dropped item.
        if (itemObjectReference.TryGet(out NetworkObject itemObj))
        {
            // Retrieve battery data (if applicable).
            BatteryItem droppedBattery = itemObj.GetComponentInChildren<BatteryItem>();
            if (droppedBattery != null && !string.IsNullOrEmpty(droppedBattery.uniqueItemID))
            {
                existingUniqueID = droppedBattery.uniqueItemID;
                existingBattery = droppedBattery.currentBatteryLevel;
            }
            // Retrieve the base Item component to get the lastOwnerId.
            Item droppedItem = itemObj.GetComponent<Item>();
            if (droppedItem != null)
            {
                droppedItemOwnerId = droppedItem.lastOwnerId;
                Debug.Log($"[InventoryManager] Retrieved dropped item lastOwnerId: {droppedItemOwnerId}");
            }
        }

        // Create new ItemData for the inventory slot.
        ItemData data = new ItemData();
        data.itemID = new FixedString64Bytes(itemID);
        data.itemName = new FixedString64Bytes(itemEntry.itemName);
        if (!string.IsNullOrEmpty(existingUniqueID))
        {
            data.uniqueItemID = new FixedString64Bytes(existingUniqueID);
            data.batteryLevel = existingBattery;
        }
        else
        {
            data.uniqueItemID = new FixedString64Bytes(System.Guid.NewGuid().ToString());
            data.batteryLevel = 100f;
        }
        // Set ownerId from the dropped item if available; otherwise use playerId.
        data.ownerId = (droppedItemOwnerId != 0) ? droppedItemOwnerId : playerId;

        playerInventory.inventory[slotIndex] = data;

        // Despawn the world instance of the item.
        if (itemObjectReference.TryGet(out NetworkObject itemObject))
        {
            ulong objectId = itemObject.NetworkObjectId;
            despawnedItems.Add(objectId);
            itemObject.Despawn(true);
            DespawnItemClientRpc(objectId);
        }

        playerInventory.ForceUpdateHotbarClientRpc();
        ForceUpdateHandForAllClientsClientRpc(playerId, slotIndex);
        
    }

    public bool CurrentHeldItemIsFlashlight()
    {
        var entry = ItemDatabase.Instance.GetItem(inventory[selectedSlot.Value].itemID.ToString());
        return entry != null && entry.itemName == "FlashLight";
        // or check by your flashlight’s unique ID
    }



    [ClientRpc]
    private void ForceUpdateHandForAllClientsClientRpc(ulong playerId, int slotIndex)
    {
        InventoryManager[] players = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.OwnerClientId == playerId)
            {
                Debug.Log($"Forcing hand update for Player {playerId} on all clients.");
                player.UpdateHeldItem();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropItemServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (inventory.Count <= slotIndex || inventory[slotIndex].IsEmpty)
        {
            Debug.Log("No item to drop.");
            return;
        }

        // Get the stored battery level from inventory data.
        float currentBattery = inventory[slotIndex].batteryLevel;
        if (currentHeldItem != null)
        {
            BatteryItem battery = currentHeldItem.GetComponentInChildren<BatteryItem>();
            if (battery != null)
                currentBattery = battery.currentBatteryLevel;
        }

        // Retrieve the ItemData from inventory and update its battery level.
        ItemData data = inventory[slotIndex];
        data.batteryLevel = currentBattery;
        inventory[slotIndex] = data;

        // Instantiate the dropped item using the prefab from the database.
        ItemDatabase.ItemEntry entry = ItemDatabase.Instance.GetItem(inventory[slotIndex].itemID.ToString());
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("Item missing prefab. Cannot drop.");
            return;
        }

        Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
        GameObject droppedItem = Instantiate(entry.prefab, dropPosition, Quaternion.identity);
        NetworkObject netObj = droppedItem.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();

        // Transfer battery data to the dropped item (if applicable).
        BatteryItem droppedBattery = droppedItem.GetComponentInChildren<BatteryItem>();
        if (droppedBattery != null)
        {
            droppedBattery.uniqueItemID = data.uniqueItemID.ToString();
            droppedBattery.initialBatteryLevel = 100f;
            droppedBattery.currentBatteryLevel = data.batteryLevel;
            droppedBattery.ConfigureSlider();
            droppedBattery.SetHeld(false);
        }
        else
        {
            Debug.LogWarning("Dropped item has no BatteryItem component!");
        }

        // *** NEW CODE: Transfer the owner information ***
        Item droppedItemScript = droppedItem.GetComponent<Item>();
        if (droppedItemScript != null)
        {
            // Copy the owner ID from the stored data into the new dropped item.
            droppedItemScript.lastOwnerId = data.ownerId;
            Debug.Log($"[InventoryManager] Dropped item now has lastOwnerId = {droppedItemScript.lastOwnerId}");
        }
        else
        {
            Debug.LogWarning("Dropped item has no Item component to update owner ID.");
        }

        // Clear the inventory slot.
        Item heldItemScript = currentHeldItem.GetComponent<Item>();
        inventory[slotIndex] = new ItemData();
        if (heldItemScript != null)
        {
            heldItemScript.isHeld = false;
        }

        ForceUpdateHotbarClientRpc();
        ForceUpdateHandClientRpc(slotIndex);
    }



    private void SaveCurrentHeldBatteryLevel()
    {
        if (currentHeldItem != null)
        {
            BatteryItem heldBattery = currentHeldItem.GetComponentInChildren<BatteryItem>();
            if (heldBattery != null)
            {
                int slot = selectedSlot.Value;
                if (slot < inventory.Count)
                {
                    ItemData data = inventory[slot];
                    // Update only if the battery value has changed noticeably.
                    if (Mathf.Abs(data.batteryLevel - heldBattery.currentBatteryLevel) > 0.1f)
                    {
                        data.batteryLevel = heldBattery.currentBatteryLevel;
                        inventory[slot] = data;
                        Debug.Log($"[InventoryManager] Saved held battery level for slot {slot}: {data.batteryLevel}");
                    }
                }
            }
        }
    }



    [ClientRpc]
    private void ForceUpdateHandClientRpc(int slotIndex)
    {
        if (!IsOwner) return;
        if (inventory.Count <= slotIndex || inventory[slotIndex].IsEmpty)
        {
            ClearHand();
            return;
        }
        UpdateHeldItem();
    }

    void UpdateHeldItem()
    {
        if (inventory.Count <= selectedSlot.Value)
        {
            Debug.LogWarning("Inventory not synced yet. Preventing hand update.");
            return;
        }
        if (inventory[selectedSlot.Value].IsEmpty)
        {
            Debug.Log("Hotbar slot is empty. Clearing hand.");
            ClearHand();
            return;
        }

        ItemDatabase.ItemEntry entry = ItemDatabase.Instance.GetItem(inventory[selectedSlot.Value].itemID.ToString());
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("Item missing prefab. Clearing hand.");
            ClearHand();
            return;
        }

        if (currentHeldItem != null)
        {
            Destroy(currentHeldItem);
        }

        // Instantiate new held item.
        currentHeldItem = Instantiate(entry.prefab, handItemParent.transform);
        // Remove unnecessary network components.
        foreach (var desp in currentHeldItem.GetComponentsInChildren<ItemFallDespawn>(true))
        {
            Destroy(desp);
        }
        var netObj = currentHeldItem.GetComponent<NetworkObject>();
        if (netObj != null)
            Destroy(netObj);
        foreach (var nb in currentHeldItem.GetComponentsInChildren<NetworkBehaviour>(true))
        {
            Destroy(nb);
        }
        
        currentHeldItem.transform.localPosition = Vector3.zero;
        currentHeldItem.transform.localRotation = Quaternion.identity;
        currentHeldItem.transform.localScale = Vector3.one;

        // If the item has battery data, set it from the stored data.
        BatteryItem battery = currentHeldItem.GetComponentInChildren<BatteryItem>();
        if (battery != null)
        {
            ItemData data = inventory[selectedSlot.Value];
            battery.uniqueItemID = data.uniqueItemID.ToString();
            battery.currentBatteryLevel = data.batteryLevel;
            battery.isLocalHolder = (OwnerClientId == NetworkManager.Singleton.LocalClientId);
            battery.ConfigureSlider();
            battery.inUse(false);
            battery.SetHeld(true);
        }

        // Apply item owner info to the new instance.
        Item itemScript = currentHeldItem.GetComponent<Item>();
        if (itemScript != null)
        {
            ItemData data = inventory[selectedSlot.Value];
            itemScript.lastOwnerId = data.ownerId;
            itemScript.itemID = data.itemID.ToString();
            itemScript.isHeld = true;
        }

        Debug.Log($"[InventoryManager] New in-hand item has lastOwnerId = {itemScript?.lastOwnerId}");
    }

    private void ClearHand()
    {
        if (currentHeldItem != null)
        {
            if (currentHeldItem.TryGetComponent<WalkieTalkie>(out WalkieTalkie wt))
            {
                wt.OnDrop();
            }

            // Loop backwards through all children of handItemParent.
            for (int i = handItemParent.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = handItemParent.transform.GetChild(i).gameObject;
                var netObj = child.GetComponent<NetworkObject>();

                if (netObj != null && netObj.IsSpawned)
                {
                    // On non-host clients, avoid destroying networked objects.
                    if (!NetworkManager.Singleton.IsHost)
                    {
                        // Detach the object (so that if it later gets despawned by the host, this client won't try to destroy it)
                        child.transform.SetParent(null);
                        // Disable it so it’s no longer visible or interactive.
                        child.SetActive(false);
                    }
                    else
                    {
                        // On the host, safely despawn and destroy the network object.
                        netObj.Despawn();
                        Destroy(child);
                    }
                }
                else
                {
                    // For objects without a NetworkObject or not spawned, normal destroy is safe.
                    Destroy(child);
                }
            }
            currentHeldItem = null;
        }
        
    }

    [ClientRpc]
    private void ForceUpdateHandClientRpc(int slotIndex, ClientRpcParams clientRpcParams = default)
    {
        StartCoroutine(WaitForInventorySync(slotIndex));
    }

    private IEnumerator WaitForInventorySync(int slotIndex)
    {
        Debug.Log("Waiting for inventory sync before updating hand...");
        float timeout = 2.0f;
        float elapsedTime = 0f;

        while (inventory.Count <= slotIndex || inventory[slotIndex].IsEmpty)
        {
            if (elapsedTime >= timeout)
            {
                Debug.LogWarning("Inventory sync timeout. Hand update aborted.");
                yield break;
            }
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        Debug.Log("Inventory synced. Updating hand now.");
        UpdateHeldItem();
    }

    [ClientRpc]
    private void ForceUpdateHotbarClientRpc()
    {
        if (IsOwner && hotbarUI != null)
        {
            hotbarUI.UpdateUI(inventory);
        }
    }

    private InventoryManager FindPlayerInventoryById(ulong playerId)
    {
        InventoryManager[] players = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.GetComponent<NetworkObject>().OwnerClientId == playerId)
            {
                return player;
            }
        }
        return null;
    }
    private void DropItemWithForceRequest(float forcePercent)
    {
        Vector3 camForward = cam.transform.forward;
        Vector3 camPos = cam.transform.position;
        int layerMask = ~LayerMask.GetMask("Player"); // Avoid self-hit

        float maxDistance = 1.5f;
        Vector3 dropPoint = camPos + camForward * maxDistance;

        if (Physics.Raycast(camPos, camForward, out RaycastHit hit, maxDistance, layerMask))
        {
            dropPoint = hit.point;
        }

        DropItemWithForceServerRpc(selectedSlot.Value, dropPoint, camForward, forcePercent);
    }
    [ServerRpc(RequireOwnership = false)]
    private void DropItemWithForceServerRpc(int slotIndex, Vector3 dropPoint, Vector3 throwDirection, float forcePercent)
    {
        if (inventory.Count <= slotIndex || inventory[slotIndex].IsEmpty)
            return;

        ItemData data = inventory[slotIndex];

        ItemDatabase.ItemEntry entry = ItemDatabase.Instance.GetItem(data.itemID.ToString());
        if (entry == null || entry.prefab == null)
            return;

        GameObject droppedItem = Instantiate(entry.prefab, dropPoint, Quaternion.identity);
        NetworkObject netObj = droppedItem.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();

        // Set battery and owner data
        BatteryItem battery = droppedItem.GetComponentInChildren<BatteryItem>();
        if (battery != null)
        {
            battery.uniqueItemID = data.uniqueItemID.ToString();
            battery.initialBatteryLevel = 100f;
            battery.currentBatteryLevel = data.batteryLevel;
            battery.ConfigureSlider();
            battery.SetHeld(false);
        }

        Item itemScript = droppedItem.GetComponent<Item>();
        if (itemScript != null)
        {
            itemScript.lastOwnerId = data.ownerId;
        }

        // Throw logic
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float minForce = 1.6f;
            float maxForce = 10f;

            // Make sure forcePercent is within 0 to 1
            forcePercent = Mathf.Clamp01(forcePercent);

            // Apply easing for smoother low-charge behavior (optional)
            float easedForcePercent = Mathf.Pow(forcePercent, 1.5f); // makes ramp-up smoother

            float throwForce = Mathf.Lerp(minForce, maxForce, easedForcePercent);

            // Ensure mass affects how far things go (Impulse mode is fine here)
            rb.linearVelocity = Vector3.zero; // reset any prior movement
            rb.AddForce(throwDirection.normalized * throwForce, ForceMode.Impulse);
        }

        inventory[slotIndex] = new ItemData(); // Clear slot
        ForceUpdateHotbarClientRpc();
        ForceUpdateHandClientRpc(slotIndex);
    }
    /// <summary>
    /// Empties every hotbar slot (on the server), then notifies all clients.
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void ClearInventoryServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // 1) Reset the list to all-empty
        inventory.Clear();
        for (int i = 0; i < hotbarSize; i++)
            inventory.Add(new ItemData());

        // 2) Refresh every client’s UI
        ForceUpdateHotbarClientRpc();

        // 3) Force everyone to update the hand for this player
        ForceUpdateHandForAllClientsClientRpc(OwnerClientId, selectedSlot.Value);
    }
}
