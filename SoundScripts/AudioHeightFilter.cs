using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioHeightFilter : MonoBehaviour
{
    /// <summary>
    /// All sources above this Y will only be audible to listeners above it,
    /// and vice versa.
    /// </summary>
    public static float barrierY = 1f;

    private AudioSource _src;
    private Transform _listener;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        // assumes your main scene has exactly one AudioListener
        _listener = FindObjectOfType<AudioListener>().transform;
    }

    void Update()
    {
        // if the listener or source hasn’t been set yet, bail
        if (_listener == null || _src == null) return;

        bool sourceAbove = transform.position.y > barrierY;
        bool listenerAbove = _listener.position.y > barrierY;

        // mute whenever they are on opposite sides
        _src.mute = (sourceAbove != listenerAbove);
    }
}
