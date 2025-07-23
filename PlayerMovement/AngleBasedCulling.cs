using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AngleBasedCulling : MonoBehaviour
{
    [Tooltip("Name of the layer we want to hide when at certain angle")]
    public string hiddenLayerName = "HiddenToSelf";
    [Tooltip("World position to test against (e.g. the head bone)")]
    public Transform targetPoint;
    [Tooltip("Hide when the camera looks closer than this angle (in degrees)")]
    public float hideBelowAngle = 30f;

    Camera cam;
    int hiddenLayerMask;
    int originalMask;

    void Start()
    {
        cam = GetComponent<Camera>();
        hiddenLayerMask = LayerMask.NameToLayer(hiddenLayerName);
        originalMask = cam.cullingMask;
    }

    void LateUpdate()
    {
        if (targetPoint == null) return;

        // Direction from camera to the point
        Vector3 toTarget = (targetPoint.position - cam.transform.position).normalized;
        // Angle between where the cam is pointing and that direction
        float angle = Vector3.Angle(cam.transform.forward, toTarget);

        if (angle < hideBelowAngle)
        {
            // hide the layer
            cam.cullingMask = originalMask & ~(1 << hiddenLayerMask);
        }
        else
        {
            // show the layer again
            cam.cullingMask = originalMask;
        }
    }
}
