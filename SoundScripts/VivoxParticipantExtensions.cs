using UnityEngine;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine.Audio;

public static class VivoxParticipantExtensions
{
    /// <summary>
    /// Creates and returns a GameObject with MyVivoxParticipantTap attached,
    /// initializing it with the given VivoxParticipant and mixer group.
    /// </summary>
    public static GameObject CreateMyVivoxParticipantTap(this VivoxParticipant participant, AudioMixerGroup mixerGroup)
    {
        GameObject tapObj = new GameObject("VivoxParticipantTap_" + participant.DisplayName);
        MyVivoxParticipantTap tap = tapObj.AddComponent<MyVivoxParticipantTap>();
        tap.Initialize(participant, mixerGroup);
        return tapObj;
    }
}
