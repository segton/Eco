using UnityEngine;
using UnityEngine.Audio;
using Unity.Services.Vivox;
using System.Collections.Generic;

public class VivoxAudioManager : MonoBehaviour
{
    public static VivoxAudioManager Instance;

    public AudioMixer audioMixer;
    public AudioMixerGroup proximityGroup;
    public AudioMixerGroup walkieTalkieGroup;
    public AudioMixerGroup deadGroup;
    private string walkieChannelName;
    private Dictionary<ulong, AudioSource> playerAudioSources = new Dictionary<ulong, AudioSource>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Assigns an individual AudioSource to a player.
    /// </summary>
    public void RegisterPlayer(ulong playerId, AudioSource playerSource)
    {
        if (!playerAudioSources.ContainsKey(playerId))
        {
            playerAudioSources[playerId] = playerSource;
        }
    }

    /// <summary>
    /// Applies the correct audio effect based on the player's channel.
    /// </summary>
    public void SetAudioEffect(ulong playerId, string channelName)
    {
        if (!playerAudioSources.TryGetValue(playerId, out AudioSource source))
        {
            Debug.LogWarning($"[VivoxAudioManager] No AudioSource found for player {playerId}");
            return;
        }

        if (channelName == VoiceChannelManager.proximityChannelName)
        {
            source.outputAudioMixerGroup = proximityGroup;
            Debug.Log($"[VivoxAudioManager] Player {playerId} - Proximity chat with echo.");
        }
        else if (channelName == walkieChannelName)
        {
            source.outputAudioMixerGroup = walkieTalkieGroup;
            Debug.Log($"[VivoxAudioManager] Player {playerId} - Walkie talkie static effect.");
        }
        else if (channelName == VoiceChannelManager.deadChannelName)
        {
            source.outputAudioMixerGroup = deadGroup;
            Debug.Log($"[VivoxAudioManager] Player {playerId} - Dead channel, clean audio.");
        }
    }
}
