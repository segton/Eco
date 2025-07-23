using UnityEngine;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine.Audio;
using Unity.Services.Vivox;

[RequireComponent(typeof(AudioSource))]
public class ReliableWalkieTap : MonoBehaviour
{
    [Tooltip("Prefix of your walkie-talkie channel URIs")]
    [SerializeField] string channelNamePrefix = "WalkieTalkieChannel";

    [Tooltip("Your VoiceChat mixer group")]
    [SerializeField] AudioMixerGroup voiceMixerGroup;

    // the Vivox tap component
    VivoxChannelAudioTap _tap;
    // the AudioSource that Vivox writes into
    AudioSource _src;

    void Awake()
    {
        // grab and configure the AudioSource
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;  // we'll drive Play() manually
        _src.loop = true;   // ensure looping once Vivox sets the clip
        _src.spatialBlend = 0f;     // 2D, so distance doesn't kill volume
        _src.bypassEffects = true;   // no filters here
        _src.bypassListenerEffects = true;
        _src.bypassReverbZones = true;
        if (voiceMixerGroup != null)
            _src.outputAudioMixerGroup = voiceMixerGroup;

        // grab or add the Vivox tap, and disable autoacquire
        _tap = GetComponent<VivoxChannelAudioTap>()
               ?? gameObject.AddComponent<VivoxChannelAudioTap>();
        _tap.AutoAcquireChannel = false;
    }

    void Update()
    {
        if (VivoxService.Instance == null)
            return;

        // find & hook up the full channel URI that matches our prefix
        foreach (var kv in VivoxService.Instance.ActiveChannels)
        {
            if (kv.Key.StartsWith(channelNamePrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                _tap.ChannelName = kv.Key;

                // as soon as Vivox has assigned its streaming clip to _src.clip:
                if (_src.clip != null)
                {
                    // enforce looping (Vivox can override it)
                    _src.loop = true;
                    // restart playback if it ever stopped
                    if (!_src.isPlaying)
                        _src.Play();
                }
                break;
            }
        }

        // in case clip gets reassigned later (e.g., channel state changes),
        // keep forcing loop+play every frame once a clip exists
        if (_src.clip != null)
        {
            if (!_src.loop) _src.loop = true;
            if (!_src.isPlaying) _src.Play();
        }
    }
}
