using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    public AudioClip runningClip;
    public AudioClip jumpingClip;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.loop = true;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 30f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    public void PlayRunningSound()
    {
        if (audioSource.clip != runningClip)
        {
            audioSource.clip = runningClip;
            audioSource.Play();
        }
    }

    public void StopRunningSound()
    {
        if (audioSource.isPlaying && audioSource.clip == runningClip)
        {
            audioSource.Stop();
        }
    }

    public void PlayJumpSound()
    {
        // For one-shot effects
        AudioSource.PlayClipAtPoint(jumpingClip, transform.position, 1f);
    }
}
