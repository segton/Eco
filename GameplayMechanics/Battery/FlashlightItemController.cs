using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BatteryItem))]
public class FlashlightItemController : NetworkBehaviour
{
    private BatteryItem _battery;
    private PlayerFlashlightController _playerFlash;

    public override void OnNetworkSpawn()
    {
        // 1) Only run on the local player's copy
        if (!IsOwner) return;

        _playerFlash = GetComponentInParent<PlayerFlashlightController>();
        if (_playerFlash == null)
        {
            Debug.LogError("FlashlightItemController: no PlayerFlashlightController found in parents!");
            return;
        }

        _battery = GetComponent<BatteryItem>();
        if (_battery == null)
            Debug.LogError("FlashlightItemController: missing BatteryItem!");

        // 2) Subscribe *only* for your toggles
        _playerFlash.FlashlightOn.OnValueChanged += OnFlashlightToggled;

        // 3) Sync initial state
        OnFlashlightToggled(false, _playerFlash.FlashlightOn.Value);
    }

    public override void OnNetworkDespawn()
    {
        // clean up
        if (IsOwner && _playerFlash != null)
            _playerFlash.FlashlightOn.OnValueChanged -= OnFlashlightToggled;
    }

    private void OnFlashlightToggled(bool oldValue, bool newValue)
    {
        Debug.Log($"[Local] Flashlight toggled to {newValue}");
        _battery?.inUse(newValue);
    }
}
