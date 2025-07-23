using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
using Unity.Netcode;

public class DeadStateManager : NetworkBehaviour
{
    // These will be assigned at runtime.
    private string deadChannelName;
    private string proximityChannelName;

    private async void Start()
    {
        // Wait until VoiceChannelManager has set up the channels.
        while (string.IsNullOrEmpty(VoiceChannelManager.deadChannelName) ||
               string.IsNullOrEmpty(VoiceChannelManager.proximityChannelName))
        {
            await Task.Delay(100);
        }
        deadChannelName = VoiceChannelManager.deadChannelName;
        proximityChannelName = VoiceChannelManager.proximityChannelName;
    }
           /// <summary>
        /// Called from outside (e.g. DeadStateManager) when the server tells us to wake back up.
        /// </summary>

/// <summary>
/// Called when the player dies.
/// Disables proximity transmission and enables the dead channel.
/// </summary>
public async Task EnterDeadState()
    {
        Debug.Log("[DeadStateManager] Entering dead state...");
        try
        {
            if (!string.IsNullOrEmpty(proximityChannelName) &&
                VivoxService.Instance.ActiveChannels.ContainsKey(proximityChannelName))
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None, proximityChannelName);
            }
            else
            {
                Debug.LogError($"[DeadStateManager] Proximity channel '{proximityChannelName}' not found.");
            }

            if (!string.IsNullOrEmpty(deadChannelName) &&
                VivoxService.Instance.ActiveChannels.ContainsKey(deadChannelName))
            {
                await VivoxService.Instance.SetChannelVolumeAsync(deadChannelName, 0);
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, deadChannelName);
                Debug.Log("[DeadStateManager] Dead channel activated; proximity transmission disabled for dead state.");
            }
            else
            {
                Debug.LogError($"[DeadStateManager] Dead channel '{deadChannelName}' not found.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DeadStateManager] Error entering dead state: {ex.Message}");
        }
    }

    [ClientRpc]
    public void ReviveClientRpc(ClientRpcParams rpcParams = default)
    {
        // exactly what your J–key handler does:
        Debug.Log("Reviving player");
        TryReviveLocalPlayer();
    }

    /// <summary>
    /// Called when the player revives.
    /// Re-enables proximity transmission and mutes the dead channel.
    /// </summary>
    public async Task ExitDeadState()
    {
        Debug.Log("[DeadStateManager] Exiting dead state...");
        try
        {
            if (!string.IsNullOrEmpty(deadChannelName) &&
                VivoxService.Instance.ActiveChannels.ContainsKey(deadChannelName))
            {
                await VivoxService.Instance.SetChannelVolumeAsync(deadChannelName, -80);
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None, deadChannelName);
            }
            else
            {
                Debug.LogError($"[DeadStateManager] Dead channel '{deadChannelName}' not found during exit.");
            }

            if (!string.IsNullOrEmpty(proximityChannelName) &&
                VivoxService.Instance.ActiveChannels.ContainsKey(proximityChannelName))
            {
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, proximityChannelName);
                Debug.Log("[DeadStateManager] Proximity restored; dead channel muted on exit.");
            }
            else
            {
                Debug.LogError($"[DeadStateManager] Proximity channel '{proximityChannelName}' not found during exit.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DeadStateManager] Error exiting dead state: {ex.Message}");
        }
    }

    private void Update()
    {
        // For testing: press R to enter dead state, T to exit.
        if (Input.GetKeyDown(KeyCode.R))
            _ = EnterDeadState();
        if (Input.GetKeyDown(KeyCode.T))
            _ = ExitDeadState();
        if (Input.GetKeyDown(KeyCode.J))
            TryReviveLocalPlayer();
    }
    void TryReviveLocalPlayer()
    {
        var nm = NetworkManager.Singleton;
        var clientId = nm.LocalClientId;
        if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return;

        var pm = client.PlayerObject.GetComponent<PlayerMovement>();
        if (pm == null || pm.Health.Value > 0) return;

        // 1) Tell the server to revive you
        pm.ReviveRequestServerRpc();
        Debug.Log("[DeadStateManager] Sent ReviveRequestServerRpc()");
        // 2) Locally restore your dead-state & camera
        PlayerStateManager.Instance.SetPlayerDeadStatus(clientId.ToString(), false);
        client.PlayerObject.GetComponent<SpectatorController>()?.ExitSpectatorMode();
        Debug.Log("[DeadStateManager] Exited spectator mode");

         // 3) Now force your PlayerMovement to run its local revive code
        pm.OnRevivedClient();
    }
}
