using UnityEngine;
using UnityEngine.Audio;
using System.Threading.Tasks;

public class VivoxAudioRouting : MonoBehaviour
{
    [Header("Audio Mixer Settings")]
    public AudioMixer mixer;  // Assign your VivoxMixer asset here
    public AudioMixerSnapshot proximitySnapshot; // For proximity chat (default)
    public AudioMixerSnapshot walkieSnapshot;    // For walkie-talkie audio effects
    public AudioMixerSnapshot deadSnapshot;      // For dead channel

    [Header("Transition Settings")]
    public float transitionTime = 0.5f;

    /// <summary>
    /// Switches the local output routing to proximity mode.
    /// </summary>
    public void SetProximityMode()
    {
        proximitySnapshot.TransitionTo(transitionTime);
        Debug.Log("Audio routed to Proximity mode.");
    }

    /// <summary>
    /// Switches the local output routing to walkie-talkie mode.
    /// </summary>
    public void SetWalkieMode()
    {
        walkieSnapshot.TransitionTo(transitionTime);
        Debug.Log("Audio routed to Walkie-Talkie mode.");
    }

    /// <summary>
    /// Switches the local output routing to dead channel mode.
    /// </summary>
    public void SetDeadMode()
    {
        deadSnapshot.TransitionTo(transitionTime);
        Debug.Log("Audio routed to Dead Channel mode.");
    }
}
