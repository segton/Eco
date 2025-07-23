using UnityEngine;

[RequireComponent(typeof(BatteryItem))]
[RequireComponent(typeof(NavigatorBeacon))]
[RequireComponent(typeof(LineRenderer))]
public class NavigationGadget : MonoBehaviour
{
    private BatteryItem battery;
    private NavigatorBeacon beacon;
    private LineRenderer lineRenderer;
    private bool isOn;

    void Awake()
    {
        battery = GetComponent<BatteryItem>();
        beacon = GetComponent<NavigatorBeacon>();
        lineRenderer = GetComponent<LineRenderer>();

        // start fully off
        isOn = false;
        beacon.enabled = false;
        lineRenderer.enabled = false;    //  make sure it's off
        lineRenderer.positionCount = 0;
    }

    void Update()
    {
        if (!battery.isLocalHolder) return;
        // if we drop the item or another client has it, force off
        bool inInventory = GetComponentInParent<InventoryManager>() != null;
        if (!inInventory)
        {
            // if dropped, always turn it fully off (hides line + UI + stops battery)
            if (isOn) TurnOff();
            return;
        }

        // 1) Now that we know it’s in the inventory, check for the toggle key
        if (Input.GetKeyDown(KeyCode.V))
        {
            if (isOn) TurnOff();
            else TurnOn();
        }

        // 2) If battery dies while on, turn off
        if (isOn && battery.currentBatteryLevel <= 0f)
            TurnOff();
    }

    private void TurnOn()
    {
        if (battery.currentBatteryLevel <= 0f) return;
        isOn = true;
        battery.inUse(true);      // start draining
        beacon.enabled = true;
        lineRenderer.enabled = true;   //  show the line
        beacon.ShowTargetCanvas();
    }

    private void TurnOff()
    {
        isOn = false;
        battery.inUse(false);     // stop draining
        beacon.enabled = false;
        lineRenderer.enabled = false;  //  hide it
        lineRenderer.positionCount = 0;
        beacon.HideDestinationUI();
        beacon.HideTargetCanvas();
    }

}
