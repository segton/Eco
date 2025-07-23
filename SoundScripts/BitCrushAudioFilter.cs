using UnityEngine;

public class BitCrushAudioFilter : MonoBehaviour
{
    [SerializeField, Range(1, 16)] private int _bitDepth = 16;
    [SerializeField, Range(1, 32)] private int _sampleRateReduction = 1;
    private void OnAudioFilterRead(float[] data, int channels)
    {
        float lastSample = 0f;
        int dataLength = data.Length;

        for (int i = 0; i < dataLength; i++) 
        { 
            if (i % _sampleRateReduction == 0)
            {
                lastSample = data[i];
            }
            else
            {
                data[i] = lastSample;
            }
        
        }
    }
}
