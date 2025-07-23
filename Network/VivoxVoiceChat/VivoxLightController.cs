using UnityEngine;
using Unity.Netcode;
using Unity.Services.Vivox;

public class VivoxLightController : NetworkBehaviour
{
    [Header("Channel Settings")]
    [Tooltip("Only use your own audio energy from this channel")]
    public string channelToTrack = "ProximityVoice";

    [Header("Light Settings")]
    [Tooltip("All lights to pulse with your voice")]
    public Light[] headLights;
    public float minIntensity = 0.5f;
    public float maxIntensity = 2f;

    private NetworkVariable<float> signalStrength =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    VivoxParticipant _selfParticipant;

    public override void OnNetworkSpawn()
    {
        signalStrength.OnValueChanged += OnSignalStrengthChanged;
        if (IsOwner)
        {
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }
    }

    public override void OnNetworkDespawn()
    {
        signalStrength.OnValueChanged -= OnSignalStrengthChanged;
        if (IsOwner && _selfParticipant != null)
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            _selfParticipant.ParticipantAudioEnergyChanged -= OnAudioEnergyChanged;
            _selfParticipant = null;
        }
    }

    void OnParticipantAdded(VivoxParticipant p)
    {
        // Only your own participant in the chosen channel
        if (p.IsSelf && p.ChannelName.StartsWith(channelToTrack))
        {
            _selfParticipant = p;
            p.ParticipantAudioEnergyChanged += OnAudioEnergyChanged;
        }
    }

    void OnParticipantRemoved(VivoxParticipant p)
    {
        if (_selfParticipant == p)
        {
            p.ParticipantAudioEnergyChanged -= OnAudioEnergyChanged;
            _selfParticipant = null;
        }
    }

    void OnAudioEnergyChanged()
    {
        if (!IsOwner || _selfParticipant == null) return;
        float energy = Mathf.Clamp01((float)_selfParticipant.AudioEnergy);
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, energy);
        signalStrength.Value = intensity;
    }

    void OnSignalStrengthChanged(float oldVal, float newVal)
    {
        foreach (var light in headLights)
        {
            if (light == null) continue;
            light.intensity = newVal;
            light.enabled = newVal > (minIntensity * 0.5f);
        }
    }
}
