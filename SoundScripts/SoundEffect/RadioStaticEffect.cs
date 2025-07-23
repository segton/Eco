using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RadioStaticEffect : MonoBehaviour
{
    private AudioSource audioSource;
    private float[] noiseData;
    public float noiseVolume = 0.2f;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        noiseData = new float[44100]; // 1-second buffer
        GenerateStatic();
        PlayStatic();
    }

    void GenerateStatic()
    {
        for (int i = 0; i < noiseData.Length; i++)
        {
            noiseData[i] = Random.Range(-1f, 1f) * noiseVolume;
        }
    }

    void PlayStatic()
    {
        AudioClip noiseClip = AudioClip.Create("RadioStatic", noiseData.Length, 1, 44100, false);
        noiseClip.SetData(noiseData, 0);
        audioSource.clip = noiseClip;
        audioSource.loop = true;
        audioSource.Play();
    }
}
