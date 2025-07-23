using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Services.Vivox;

public class AudioTapsManager : MonoBehaviour
{
    public static AudioTapsManager Instance;

    [Header("Mixer Settings")]
    [Tooltip("Audio Mixer Group for routing voice chat. Add your desired effects (echo, reverb, etc.) to this group.")]
    public AudioMixerGroup voiceMixerGroup;

    // Keep track of tap objects by participant.
    private Dictionary<VivoxParticipant, GameObject> participantTaps = new Dictionary<VivoxParticipant, GameObject>();

    private void Awake()
    {
        // Standard singleton pattern.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (VivoxService.Instance != null)
        {
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }
        else
        {
            Debug.LogWarning("[AudioTapsManager] VivoxService.Instance is null! Make sure Vivox is initialized.");
        }
    }

    private void OnDisable()
    {
        if (VivoxService.Instance != null)
        {
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }
    }

    private void OnParticipantAdded(VivoxParticipant participant)
    {
        Debug.Log($"[AudioTapsManager] Participant joined: {participant.DisplayName}");
        // Create the tap and assign the mixer group.
        GameObject tapObj = participant.CreateMyVivoxParticipantTap(voiceMixerGroup);
        // Optionally, parent the tap object to a common container (like this AudioTapsManager)
        tapObj.transform.parent = transform;
        participantTaps[participant] = tapObj;
    }

    private void OnParticipantRemoved(VivoxParticipant participant)
    {
        Debug.Log($"[AudioTapsManager] Participant left: {participant.DisplayName}");
        if (participantTaps.TryGetValue(participant, out GameObject tapObj))
        {
            Destroy(tapObj);
            participantTaps.Remove(participant);
        }
    }
}
