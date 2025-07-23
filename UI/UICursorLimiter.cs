using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Cinemachine;

[RequireComponent(typeof(RectTransform))]
public class UICursorLimiter : MonoBehaviour
{
    [Header("Clamp Cursor to this Canvas Rect")]
    private RectTransform _rectTransform;
    private Camera _worldCamera;
    private bool _active;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Enables cursor clamping within the bounds of this RectTransform.
    /// </summary>
    public void Activate()
    {
        _active = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Disables cursor clamping.
    /// </summary>
    public void Deactivate()
    {
        _active = false;
    }

    void LateUpdate()
    {
        if (!_active)
            return;

        // Ensure we have a valid camera: grab the CinemachineBrain output if needed
        if (_worldCamera == null)
        {
            var brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain != null && brain.OutputCamera != null)
                _worldCamera = brain.OutputCamera;
        }
        if (_worldCamera == null)
            return;

        // Get world-space corners of the RectTransform
        Vector3[] worldCorners = new Vector3[4];
        _rectTransform.GetWorldCorners(worldCorners);

        // Convert to screen-space corners
        Vector2 min = _worldCamera.WorldToScreenPoint(worldCorners[0]);
        Vector2 max = _worldCamera.WorldToScreenPoint(worldCorners[2]);

        // Read current pointer position
        var mouse = Mouse.current;
        if (mouse == null)
            return;
        Vector2 pos = mouse.position.ReadValue();

        // Clamp
        float clampedX = Mathf.Clamp(pos.x, min.x, max.x);
        float clampedY = Mathf.Clamp(pos.y, min.y, max.y);

        // If outside, warp it back in
        if (clampedX != pos.x || clampedY != pos.y)
        {
            mouse.WarpCursorPosition(new Vector2(clampedX, clampedY));
        }
    }
}
