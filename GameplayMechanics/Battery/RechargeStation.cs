using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class RechargeStation : MonoBehaviour
{
    // If > 0, adds that much charge; if <= 0, refills to max
    public float rechargeAmount = 0f;

    void Reset()
    {
        // Ensure this collider is a trigger
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerStay(Collider other)
    {
        // 1. Find the InventoryManager on the object or its parents
        InventoryManager inv = other.GetComponentInParent<InventoryManager>();
        if (inv == null)
        {
            Debug.Log("RechargeStation: No InventoryManager found on collider or its parents");
            return;
        }

        // 2. Ignore if it belongs to a remote player
        if (!inv.IsOwner)
        {
            Debug.Log("RechargeStation: Collider belongs to a non-local player");
            return;
        }

        Debug.Log("RechargeStation: Local player is in station");

        // 3. On F press, inspect and recharge
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("RechargeStation: F pressed, attempting to recharge");

            int slot = inv.selectedSlot.Value;
            ItemData data = inv.inventory[slot];
            Debug.Log($"RechargeStation: Slot {slot}, empty = {data.IsEmpty}, itemID = '{data.itemID}'");

            GameObject held = inv.currentHeldItem;
            Debug.Log("RechargeStation: currentHeldItem = " + (held == null ? "null" : held.name));
            if (held == null)
            {
                Debug.Log("RechargeStation: No held item");
                return;
            }

            BatteryItem battery = held.GetComponentInChildren<BatteryItem>();
            Debug.Log("RechargeStation: BatteryItem component found: " + (battery != null));
            if (battery == null)
            {
                Debug.Log("RechargeStation: Held item has no BatteryItem component");
                return;
            }

            // 4. Perform the recharge
            if (rechargeAmount > 0f)
            {
                battery.RechargeFull();
            }
            else
            {
                //battery.RechargeFull();
            }

            Debug.Log("RechargeStation: Battery now at " + battery.currentBatteryLevel);

            // 5. Persist the new battery level on the server
            inv.UpdateBatteryLevelServerRpc(slot, battery.currentBatteryLevel);
        }
    }
}
