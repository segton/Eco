using UnityEngine;

public class DebugCanvasToggle : MonoBehaviour
{
    [Tooltip("Assign the Canvas (or its root GameObject) you want to show/hide")]
    [SerializeField] private GameObject debugCanvas;

    private void Start()
    {
        if (debugCanvas == null)
            Debug.LogWarning("[DebugCanvasToggle] No Canvas assigned!", this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) && debugCanvas != null)
        {
            bool isActive = debugCanvas.activeSelf;
            debugCanvas.SetActive(!isActive);
        }
    }
}
