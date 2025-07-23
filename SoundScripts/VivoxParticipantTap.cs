using UnityEngine;
using Unity.Services.Vivox;
using UnityEngine.Audio;

/// <summary>
/// A simple component that represents a tap for a Vivox participant's voice.
/// In a complete solution, this class would pull audio data from the Vivox stream and feed it to the AudioSource.
/// Here, we assume Vivox is already routing audio into a default channel,
/// and this component exists so we can assign an AudioMixerGroup and set 3D properties.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MyVivoxParticipantTap : MonoBehaviour
{
    private AudioSource audioSource;
    private VivoxParticipant participant;

    public void Initialize(VivoxParticipant participant, AudioMixerGroup mixerGroup)
    {
        this.participant = participant;
        audioSource = GetComponent<AudioSource>();
        if (mixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = mixerGroup;
        }
        // Configure 3D audio settings
        audioSource.spatialBlend = 1.0f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 30f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        // IMPORTANT: In a full solution, here you’d subscribe to Vivox audio frame callbacks
        // and feed the audio into audioSource via OnAudioFilterRead or a similar method.
        // That code depends on Vivox’s API and your project’s setup.
    }
}
