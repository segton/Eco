using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
public class WorldSpaceHitGizmo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas.renderMode != RenderMode.WorldSpace) return;

        var rt = canvas.GetComponent<RectTransform>();
        if (rt == null) return;

        // get the four corners in world space
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Gizmos.color = Color.green;
        // draw the rectangle
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}
