using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
using Unity.Netcode;

public class WalkieTalkie : MonoBehaviour
{
    // The 2D channel name for radio.
    public string RadioChannelName;

    // Reference to the BatteryItem component.
    public BatteryItem itemUsage;

    private string proximityChannelName;
    // Flag to track if this player is currently transmitting on radio.
    private bool isTransmitting = false;

    // Expose a property so other scripts can check if transmitting.
    public bool IsTransmitting => isTransmitting;

    private void Awake()
    {
        // Auto-assign BatteryItem if not already assigned.
        if (itemUsage == null)
            itemUsage = GetComponent<BatteryItem>();
    }

    public void OnPickup()
    {
        Debug.Log("[WalkieTalkie] Picked up.");
        if (itemUsage != null)
        {
            // Mark the item as held so that the slider appears.
            itemUsage.SetHeld(true);
            // Do not set the item "in use" until the player actually presses V.
            itemUsage.inUse(false);
        }
        else
        {
            Debug.LogWarning("ItemUsage reference not assigned in WalkieTalkie.");
        }
        // Force re-sync for the host so that channel settings are correctly set.
        if (NetworkManager.Singleton.IsHost)
        {
            ForceResync();
        }
    }

    private void Start()
    {
        RadioChannelName = VoiceChannelManager.walkieChannelName;
        proximityChannelName = VoiceChannelManager.proximityChannelName;
        if (string.IsNullOrEmpty(proximityChannelName))
        {
            Debug.LogError("[WalkieTalkie] Proximity channel name is null or empty.");
            return;
        }
    }

    /// <summary>
    /// Force re-sync the channel settings so that the host's walkie talkie is set to proximity mode.
    /// This prevents the host from auto-broadcasting.
    /// </summary>
    public async void ForceResync()
    {
        try
        {
            if (VivoxService.Instance != null)
            {
                // Force the walkie channel to be in "Single" (proximity) mode.
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, RadioChannelName);
                Debug.Log("[WalkieTalkie] Force re-sync: Host walkie channel set to proximity mode.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalkieTalkie] ForceResync failed: {ex.Message}");
        }
    }

    public async void OnDrop()
    {
        if (itemUsage != null)
        {
            itemUsage.SetHeld(false); // Mark as not held.
            itemUsage.inUse(false);     // Stop draining battery.
        }
        Debug.Log("[WalkieTalkie] Dropped. Returning to proximity transmission.");
        if (isTransmitting)
        {
            StopTalk(); // Ensure we stop transmitting.
        }
        try
        {
            if (VivoxService.Instance != null)
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, proximityChannelName);
                Debug.Log("[WalkieTalkie] Transmission mode set to Single (ProximityVoice).");
            }
            else
            {
                Debug.LogWarning("VivoxService.Instance is null in OnDrop.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalkieTalkie] OnDrop failed: {ex.Message}");
        }
    }

    public async void StartTalk()
    {
        // If battery is empty, do not allow starting transmission.
        if (itemUsage != null && itemUsage.currentBatteryLevel <= 0)
        {
            Debug.Log("[WalkieTalkie] Battery empty, cannot start transmission.");
            return;
        }
        if (isTransmitting) return;
        if (RadioChannelManager.Instance == null)
        {
            Debug.LogWarning("[WalkieTalkie] No RadioChannelManager available.");
            return;
        }
        RadioChannelManager.Instance.RequestSpeakServerRpc();
        await Task.Delay(100);
        if (RadioChannelManager.Instance.CurrentRadioSpeaker.Value != NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[WalkieTalkie] Another player is already transmitting. Aborting StartTalk.");
            return;
        }
        isTransmitting = true;
        Debug.Log("[WalkieTalkie] Starting talk. Setting transmission mode to All.");
        try
        {
            if (VivoxService.Instance != null)
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.All, RadioChannelName);
                Debug.Log("[WalkieTalkie] Transmission started on radio channel.");
            }
            else
            {
                Debug.LogWarning("VivoxService.Instance is null in StartTalk.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalkieTalkie] StartTalk failed: {ex.Message}");
            isTransmitting = false;
            return;
        }
        if (itemUsage != null)
        {
            // Now mark the item as in use so that battery drain begins.
            itemUsage.SetHeld(true);
            itemUsage.inUse(true);
            Debug.Log("[WalkieTalkie] Walkie talkie is now in use.");
        }
        else
        {
            Debug.LogWarning("ItemUsage reference not assigned in WalkieTalkie.");
        }
    }

    public async void StopTalk()
    {
        if (!isTransmitting) return;
        if (RadioChannelManager.Instance == null)
        {
            Debug.LogWarning("[WalkieTalkie] No RadioChannelManager available.");
            return;
        }
        if (RadioChannelManager.Instance.CurrentRadioSpeaker.Value == NetworkManager.Singleton.LocalClientId)
        {
            RadioChannelManager.Instance.StopSpeakServerRpc();
        }
        Debug.Log("[WalkieTalkie] Stopping talk. Setting transmission mode to Single (ProximityVoice).");
        try
        {
            if (VivoxService.Instance != null)
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, proximityChannelName);
                Debug.Log("[WalkieTalkie] Transmission stopped.");
            }
            else
            {
                Debug.LogWarning("VivoxService.Instance is null in StopTalk.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WalkieTalkie] StopTalk failed: {ex.Message}");
        }
        isTransmitting = false;
        if (itemUsage != null)
        {
            itemUsage.inUse(false);  // Mark as not in use so battery stops draining.
            Debug.Log("[WalkieTalkie] Walkie talkie is no longer in use.");
        }
    }
}
