using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
using Unity.Netcode;

public class ProximityVoiceTest : NetworkBehaviour
{
    [SerializeField] private string channelName = "ProximityVoice";

    private async void Start()
    {
        if (!IsOwner) return;
        // Wait a moment to ensure VivoxGameManager has finished logging in
        await Task.Delay(1000);

        if (VivoxService.Instance == null)
        {
            Debug.LogError("[Vivox] VivoxService is null.");
            return;
        }
        if (!VivoxService.Instance.IsLoggedIn)
        {
            Debug.LogError("[Vivox] Not logged in.");
            return;
        }

        // Try joining a group channel (for testing)
        await JoinChannel(channelName);
        DebugActiveChannels();
    }

    private async Task JoinChannel(string channelName)
    {
        try
        {
            // Join as a group channel for simpler debugging
            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.AudioOnly);
            Debug.Log($"[Vivox] Joined group channel: {channelName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Vivox] Failed to join group channel: {ex.Message}");
        }
    }

    private void DebugActiveChannels()
    {
        Debug.Log("[Vivox] Active channels:");
        foreach (var kvp in VivoxService.Instance.ActiveChannels)
        {
            Debug.Log($"[Vivox] Channel: {kvp.Key}, Participants: {kvp.Value.Count}");
            foreach (var participant in kvp.Value)
            {
                Debug.Log($"[Vivox] Participant: {participant.DisplayName}");
            }
        }
    }
}
