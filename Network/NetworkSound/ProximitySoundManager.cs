using UnityEngine;
using Unity.Netcode;

public class ProximitySoundManager : NetworkBehaviour
{
    public static ProximitySoundManager Instance;

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
    /// Plays a sound effect on all clients at the specified position.
    /// </summary>
    /// <param name="soundId">ID/index of the sound in the SoundLibrary.</param>
    /// <param name="position">World position to play the sound.</param>
    [ServerRpc(RequireOwnership = false)]
    public void PlaySoundServerRpc(int soundId, Vector3 position)
    {
        PlaySoundClientRpc(soundId, position);
    }

    [ClientRpc]
    private void PlaySoundClientRpc(int soundId, Vector3 position)
    {
        AudioClip clip = SoundLibrary.Instance.GetClip(soundId);
        if (clip != null)
        {
            GameObject soundObj = new GameObject("NetworkedSound");
            soundObj.transform.position = position;
            AudioSource source = soundObj.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1.0f; // Fully 3D
            source.minDistance = 2f;
            source.maxDistance = 50f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(soundObj, clip.length + 0.5f);
        }
        else
        {
            Debug.LogWarning($"No clip found for soundId {soundId}");
        }
    }
}
