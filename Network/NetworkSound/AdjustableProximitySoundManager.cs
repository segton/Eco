using UnityEngine;
using Unity.Netcode;
using UnityEngine.Audio;

public class AdjustableProximitySoundManager : NetworkBehaviour
{
    public static AdjustableProximitySoundManager Instance;

    // Expose this curve in the Inspector so you can adjust it.
    [Tooltip("Custom attenuation curve for 3D sound drop off.")]
    public AnimationCurve rolloffCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    // Optional: assign an AudioMixerGroup if needed
    public AudioMixerGroup mixerGroup;

    private void Awake()
    {
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

    /// <summary>
    /// Plays a spatialized sound effect on all clients.
    /// </summary>
    /// <param name="soundId">ID corresponding to a sound in SoundLibrary.</param>
    /// <param name="position">World position where the sound should play.</param>
    /// <param name="minDistance">Distance at which the sound is at full volume.</param>
    /// <param name="maxDistance">Distance at which the sound fades out completely.</param>
    [ServerRpc(RequireOwnership = false)]
    public void PlaySoundServerRpc(int soundId, Vector3 position, float minDistance, float maxDistance)
    {
        PlaySoundClientRpc(soundId, position, minDistance, maxDistance);
    }

    [ClientRpc]
    private void PlaySoundClientRpc(int soundId, Vector3 position, float minDistance, float maxDistance)
    {
        AudioClip clip = SoundLibrary.Instance.GetClip(soundId);
        if (clip != null)
        {
            GameObject soundObj = new GameObject("NetworkedSound");
            soundObj.transform.position = position;
            AudioSource audioSource = soundObj.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Custom;
            // Use the curve defined in the manager (set in the Inspector)
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloffCurve);
            if (mixerGroup != null)
            {
                audioSource.outputAudioMixerGroup = mixerGroup;
            }
            audioSource.Play();
            Destroy(soundObj, clip.length + 0.5f);
        }
        else
        {
            Debug.LogWarning($"[AdjustableProximitySoundManager] No AudioClip found for soundId {soundId}");
        }
    }
}
