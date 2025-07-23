// ButtonHitGizmo.cs
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Button))]
public class ButtonHitGizmo : MonoBehaviour
{
    void OnDrawGizmos()
    {
        var rt = GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}
