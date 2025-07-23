using UnityEngine;
using Unity.Services.Vivox.AudioTaps;
using System.Linq;
using Unity.Services.Vivox;

/// <summary>
/// Automatically finds any joined Vivox channel whose URI starts with the given prefix
/// (e.g. your “walkie talkie” channel) and attaches a VivoxChannelAudioTap to it.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WalkieTalkieChannelTap : MonoBehaviour
{
    [Tooltip("The prefix of your Walkie-Talkie channel URIs (e.g. \"walkieTalkieChannel\")")]
    [SerializeField] private string channelNamePrefix = "walkieTalkieChannel";

    private bool _tapAssigned = false;
    private VivoxChannelAudioTap _tap;

    void Awake()
    {
        // Ensure there’s an AudioSource on this GameObject
        if (!TryGetComponent<AudioSource>(out _))
            gameObject.AddComponent<AudioSource>();

        // Grab or create the tap
        _tap = GetComponent<VivoxChannelAudioTap>();
        if (_tap == null)
            _tap = gameObject.AddComponent<VivoxChannelAudioTap>();

        // Disable autoacquire so it only uses the ChannelName we set
        _tap.AutoAcquireChannel = false;
    }

    void Update()
    {
        if (_tapAssigned || VivoxService.Instance == null)
            return;

        // Look through all active channels’ URIs
        foreach (var kvp in VivoxService.Instance.ActiveChannels)
        {
            string uri = kvp.Key;
            if (uri.StartsWith(channelNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                // Assign the tap to this full channel URI
                _tap.ChannelName = uri;

                _tapAssigned = true;
                Debug.Log($"[WalkieTalkieChannelTap] Attached audio tap to channel: {uri}");
                break;
            }
        }
    }

    void OnDestroy()
    {
        if (_tap != null)
            Destroy(_tap);
    }
}
