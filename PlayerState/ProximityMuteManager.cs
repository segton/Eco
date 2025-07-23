using System.Linq;
using UnityEngine;
using Unity.Services.Vivox;

public class ProximityMuteManager : MonoBehaviour
{
    public string ProximityChannelName = "ProximityVoice";

    private void Update()
    {
        if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
            return;
        if (!VivoxService.Instance.ActiveChannels.ContainsKey(ProximityChannelName))
            return;

        // Assume local player's Vivox ID is accessible.
        string localPlayerId = VivoxService.Instance.SignedInPlayerId;
        // Determine local player's dead status.
        bool localIsDead = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.IsDead : false;

        var participants = VivoxService.Instance.ActiveChannels[ProximityChannelName];
        foreach (var participant in participants)
        {
            // Skip local participant.
            if (participant.PlayerId == localPlayerId)
                continue;

            // Determine if the remote participant is dead.
            bool remoteIsDead = PlayerStateManager.Instance != null && PlayerStateManager.Instance.IsPlayerDead(participant.PlayerId);

            // For alive local players: mute any dead participant.
            if (!localIsDead && remoteIsDead)
            {
                if (!participant.IsMuted)
                    participant.MutePlayerLocally();
            }
            else
            {
                // Otherwise, ensure they are unmuted.
                if (participant.IsMuted)
                    participant.UnmutePlayerLocally();
            }
        }
    }
}
