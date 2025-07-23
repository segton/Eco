using UnityEngine;

[RequireComponent(typeof(BatteryItem))]
public class FlashlightItem : MonoBehaviour
{
    [Header("References")]
    public Light flashlight;
    // We no longer expose this in the Inspector.
    private Transform cameraTransform;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.V;
    public float emptyThreshold = 2f;

    private BatteryItem battery;
    private bool isOn = false;

    void Awake()
    {
        battery = GetComponent<BatteryItem>();

        // Auto-find the Light if you forgot to assign it:
        if (flashlight == null)
            flashlight = GetComponentInChildren<Light>();

        // Start switched off
        if (flashlight != null)
            flashlight.enabled = false;
    }

    void LateUpdate()
    {
        // Only run this on the client who actually holds it
        if (!(battery.isHeld && battery.isLocalHolder))  // battery.isLocalHolder from BatteryItem.cs :contentReference[oaicite:5]{index=5}
            return;

        // 1) Grab the local camera every frame from your InventoryManager
        var inv = GetComponentInParent<InventoryManager>();
        if (inv != null && inv.cam != null)
            cameraTransform = inv.cam.transform;

        // 2) Snap position & rotation to that camera
        if (cameraTransform != null)
        {
            transform.position = cameraTransform.position;
            transform.rotation = cameraTransform.rotation;
        }

        // 3) Handle toggle input
        if (Input.GetKeyDown(toggleKey))
        {
            if (battery.currentBatteryLevel > emptyThreshold)
            {
                isOn = !isOn;
                flashlight.enabled = isOn;
                battery.inUse(isOn);
            }
            else
            {
                Debug.Log("[FlashlightItem] Battery too low to turn on.");
            }
        }

        // 4) Auto-turn off if battery dies
        if (isOn && battery.currentBatteryLevel <= emptyThreshold)
        {
            isOn = false;
            flashlight.enabled = false;
            battery.inUse(false);
            Debug.Log("[FlashlightItem] Battery drained—flashlight off.");
        }
    }
}
