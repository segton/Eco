using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class ScrollWheelZoom : MonoBehaviour
{
    [Tooltip("How fast the camera zooms in/out per notch of scroll-wheel")]
    public float zoomSpeed = 2f;

    [Tooltip("Min/Max allowable orbit radius")]
    public float minRadius = 2f;
    public float maxRadius = 10f;

    CinemachineOrbitalFollow _orbital;

    void Awake()
    {
        // grab the vcam
        var vcam = GetComponent<CinemachineCamera>();
        // pull the Body stage component and cast it
        var comp = vcam.GetCinemachineComponent(CinemachineCore.Stage.Body);
        _orbital = comp as CinemachineOrbitalFollow;
        if (_orbital == null)
            Debug.LogError("ScrollWheelZoom needs a CinemachineOrbitalFollow on this vcam (Body stage).");
    }

    void Update()
    {
        // read scroll wheel
        float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
        if (Mathf.Approximately(scroll, 0f)) return;

        // adjust & clamp the orbit radius
        float r = _orbital.Radius - scroll * zoomSpeed;
        _orbital.Radius = Mathf.Clamp(r, minRadius, maxRadius);
    }
}
