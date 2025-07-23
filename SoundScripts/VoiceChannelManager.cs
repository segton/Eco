using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;


public class VoiceChannelManager : MonoBehaviour
{

    public static VoiceChannelManager Instance { get; private set; }

    // Base names for your channels (they will be appended with a session ID)
    public static string BaseProximityChannelName = "ProximityVoice";
    public static string BaseWalkieChannelName = "WalkieTalkieChannel";
    public static string BaseDeadChannelName = "DeadChannel";
    public bool chatDone = false;
    // Session-specific channel names (set in InitializeChannels)
    public static string proximityChannelName { get; private set; }
    public static string walkieChannelName { get; private set; }
    public static string deadChannelName { get; private set; }

    // Flag to indicate whether the walkie channel is ready
    public static bool WalkieChannelReady { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// This static method is called from the LobbyManager after the session is joined.
    /// The sessionId is appended to channel names so that separate sessions use distinct channels.
    /// </summary>
    public static void InitializeChannels(string sessionId)
    {
        if (Instance == null)
        {
            Debug.LogError("[VoiceChannelManager] No instance found in the scene!");
            return;
        }
        Instance.InitializeChannelsInstance(sessionId);
    }

    private async void InitializeChannelsInstance(string sessionId)
    {
        Debug.Log($"[VoiceChannelManager] Initializing voice channels for session: {sessionId}");

        // Create session-specific channel names:
        proximityChannelName = $"{BaseProximityChannelName}_{sessionId}";
        walkieChannelName = $"{BaseWalkieChannelName}_{sessionId}";
        deadChannelName = $"{BaseDeadChannelName}_{sessionId}";

        // Wait for Vivox to be initialized and logged in.
        while (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
        {
            await Task.Delay(100);
        }
        Debug.Log("Vivox instance hash: " + VivoxService.Instance.GetHashCode());

        // Join the proximity (3D) channel.
        try
        {
            var props = new Channel3DProperties(
                audibleDistance: 25,
                conversationalDistance: 7,
                audioFadeIntensityByDistanceaudio: 2.3f,
                audioFadeModel: AudioFadeModel.InverseByDistance
            );
            await VivoxService.Instance.JoinPositionalChannelAsync(proximityChannelName, ChatCapability.AudioOnly, props);
            Debug.Log($"[VoiceChannelManager] Joined proximity channel: {proximityChannelName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceChannelManager] Failed to join proximity: {ex.Message}");
        }

        // Ensure the local player is in the proximity channel before applying settings.
        await WaitForChannelReady(proximityChannelName);

        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(deadChannelName, ChatCapability.AudioOnly, new ChannelOptions());
            // Mute the dead channel for now.
            await VivoxService.Instance.SetChannelVolumeAsync(deadChannelName, -80);
            await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None, deadChannelName);
            Debug.Log($"[VoiceChannelManager] Joined and muted dead channel: {deadChannelName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceChannelManager] Failed to join dead channel: {ex.Message}");
        }
        // Join the walkie talkie channel.
        try
        {
            await VivoxService.Instance.JoinGroupChannelAsync(walkieChannelName, ChatCapability.AudioOnly);
            Debug.Log($"[VoiceChannelManager] Joined walkie channel: {walkieChannelName}");
            bool ready = await WaitForChannelReady(walkieChannelName);
            if (ready)
            {
                WalkieChannelReady = true;
                Debug.Log($"[VoiceChannelManager] WalkieChannel is ready.");
            }
            else
            {
                Debug.LogError($"[VoiceChannelManager] WalkieChannel failed to become ready.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VoiceChannelManager] Failed to join walkie channel: {ex.Message}");
        }

        // Join the dead channel.
        

        // Set default transmission mode to the proximity channel.
        try
        {
            await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, proximityChannelName);
            Debug.Log("[VoiceChannelManager] Default TX set to single => ProximityVoice.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VoiceChannelManager] Set default TX failed: {ex.Message}");
        }
        chatDone = true;
    }

    /// <summary>
    /// Helper method: waits until the specified channel appears in ActiveChannels.
    /// </summary>
    private async Task<bool> WaitForChannelReady(string channelName, int timeoutMs = 5000)
    {
        int waited = 0;
        int interval = 200;
        while (!VivoxService.Instance.ActiveChannels.ContainsKey(channelName) && waited < timeoutMs)
        {
            Debug.Log($"[VoiceChannelManager] Waiting for {channelName} to register... ({waited}ms)");
            await Task.Delay(interval);
            waited += interval;
        }
        return VivoxService.Instance.ActiveChannels.ContainsKey(channelName);
    }
}
