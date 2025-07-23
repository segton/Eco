using UnityEngine;
using Unity.Cinemachine;                                 // CinemachineCamera
using UnityEngine.Rendering;                             // VolumeProfile
using UnityEngine.Rendering.Universal;                   // URP/HDRP Volume

[RequireComponent(typeof(CinemachineCamera))]
public class SpectatorNightVision : MonoBehaviour
{
    [Tooltip("Assign your NightVision VolumeProfile here")]
    public VolumeProfile nightVisionProfile;

    CinemachineCamera cam;
    CinemachineVolumeSettings vol;

    void Awake()
    {
        // 1) Get (or add) the new CinemachineCamera component
        cam = GetComponent<CinemachineCamera>();
        if (cam == null)
            cam = gameObject.AddComponent<CinemachineCamera>();

        // 2) Get (or add) the VolumeSettings extension
        vol = GetComponent<CinemachineVolumeSettings>();
        if (vol == null)
            vol = gameObject.AddComponent<CinemachineVolumeSettings>();

        // 3) Use the new (no m_ prefix) properties:
        vol.Profile = nightVisionProfile;     // turn1search0
        vol.Weight = 1f;                     // full effect when live
        // no more m_IsGlobal, m_BlendDistance, or layer masks 
    }
}
