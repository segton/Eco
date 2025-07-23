using UnityEngine;

[ExecuteAlways]
public class BillboardName : MonoBehaviour
{
    [Tooltip("Drag your active, local Player Camera here")]
    public Camera targetCamera;

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // Compute the direction from the label to the camera
        Vector3 dir = transform.position - targetCamera.transform.position;

        // Optional: lock rotation to Y only
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        // Rotate so the label faces the camera
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
