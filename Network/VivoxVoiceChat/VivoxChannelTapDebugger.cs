using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VivoxTapFinalCheck : MonoBehaviour
{
    AudioSource _src;
    bool _loggedClip;

    void Awake() => _src = GetComponent<AudioSource>();

    void Update()
    {
        // 1) Did Vivox give us a clip?
        if (_src.clip != null && !_loggedClip)
        {
            Debug.Log($"[TapCheck] Got Vivox clip: {_src.clip.name} (len {_src.clip.length:F2}s)");
            _loggedClip = true;
        }

        // 2) Are we playing?
        if (_src.clip != null)
        {
            Debug.Log($"[TapCheck] isPlaying = {_src.isPlaying}");
        }
    }
}
