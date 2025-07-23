using UnityEngine;

public class Billboard : MonoBehaviour
{
    // The camera this label should face
    private Camera _targetCam;

    /// <summary>
    /// Must be called once after instantiating the label,
    /// passing in the local player's camera.
    /// </summary>
    public void Initialize(Camera cameraToFace)
    {
        _targetCam = cameraToFace;
    }

    void LateUpdate()
    {
        if (_targetCam == null)
            return;

        // Direction from label to camera, flattened on Y so label stays vertical
        Vector3 dir = transform.position - _targetCam.transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
