using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachineVolumeSettings))]
public class VCamPostProcess : MonoBehaviour
{
    [Header("Volume Profiles")]
    public VolumeProfile gameplayProfile;
    public VolumeProfile scanProfile;
    public VolumeProfile spectateProfile;

    [Header("Ambient Light Colors")]
    [Tooltip("Ambient when not scanning/spectating")]
    public Color originalAmbient = Color.gray;   // set in Inspector or read at runtime
    [Tooltip("Ambient boost for night-vision (scan)")]
    public Color scanAmbientColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [Tooltip("Ambient boost when spectating")]
    public Color spectateAmbientColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    CinemachineVolumeSettings _vol;
    Color _savedOriginalAmbient;
    bool _isSpectating;
    bool _isScanning;

    void Awake()
    {
        _vol = GetComponent<CinemachineVolumeSettings>();
        // cache whatever the scene started with
        _savedOriginalAmbient = RenderSettings.ambientLight;
        if (originalAmbient == Color.gray)
            originalAmbient = _savedOriginalAmbient;

        // start out “gameplay” look
        ApplyVolumeAndAmbient(gameplayProfile, originalAmbient);
    }

    /// <summary>
    /// Call each frame you hold/releases your scan key.
    /// </summary>
    public void SetScanMode(bool on)
    {
        _isScanning = on;
        if (on)
        {
            // scan overrides everything
            ApplyVolumeAndAmbient(scanProfile, scanAmbientColor);
        }
        else
        {
            // back to whichever base you’re in
            var baseProfile = _isSpectating ? spectateProfile : gameplayProfile;
            var baseAmbient = _isSpectating ? spectateAmbientColor : originalAmbient;
            ApplyVolumeAndAmbient(baseProfile, baseAmbient);
        }
    }

    /// <summary>
    /// Call on death (true) and on respawn (false).
    /// Note: if you’re actively scanning, scan ambient stays until you release.
    /// </summary>
    public void SetSpectateMode(bool spectating)
    {
        _isSpectating = spectating;

        // only switch base-look if you’re not scanning right now
        if (!_isScanning)
        {
            if (spectating)
                ApplyVolumeAndAmbient(spectateProfile, spectateAmbientColor);
            else
                ApplyVolumeAndAmbient(gameplayProfile, originalAmbient);
        }
    }

    /// <summary>
    /// Helper to set both at once.
    /// </summary>
    void ApplyVolumeAndAmbient(VolumeProfile prof, Color amb)
    {
        _vol.Profile = prof;
        _vol.Weight = 1f;
        RenderSettings.ambientLight = amb;
    }
}
