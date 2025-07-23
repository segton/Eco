using UnityEngine;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(RoomSetup))]
public class StartRoomDimensionMarker : MonoBehaviour
{
    [Tooltip("How long (seconds) the lines stay visible")]
    public float duration = 30f;

    [Tooltip("Color of the wireframe")]
    public Color color = Color.magenta;

    void Start()
    {
        StartCoroutine(DrawBoundsNextFrame());
    }

    IEnumerator DrawBoundsNextFrame()
    {
        // wait one frame so RoomSetup.rooms[] is ready
        yield return null;
        yield return new WaitUntil(() => GetComponent<RoomSetup>().rooms != null);

        var rs = GetComponent<RoomSetup>();
        var mb = FindObjectOfType<DungeonMeshBuilder>();
        float cs = mb != null ? mb.cellSize : 1f;

        var startRoom = rs.rooms.FirstOrDefault(r => r.roomType == RoomType.Start);
        if (startRoom == null)
        {
            Debug.LogError("[StartRoomDimensionMarker] No Start room found!");
            yield break;
        }

        // find its matching BoundsInt by index
        int idx = System.Array.IndexOf(rs.rooms, startRoom);
        var b = rs.generator.Rooms[idx];

        // world-space min & max
        Vector3 worldMin = new Vector3(b.xMin, b.yMin, b.zMin) * cs;
        Vector3 worldMax = worldMin + new Vector3(b.size.x, b.size.y, b.size.z) * cs;

        // build all 8 corners
        Vector3[] corners = new Vector3[8];
        int i = 0;
        for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
                for (int zi = 0; zi < 2; zi++)
                    corners[i++] = new Vector3(
                        xi == 0 ? worldMin.x : worldMax.x,
                        yi == 0 ? worldMin.y : worldMax.y,
                        zi == 0 ? worldMin.z : worldMax.z
                    );

        // edges to draw: bottom rectangle (0–1–3–2), top (4–5–7–6), and verticals (0–4,1–5,2–6,3–7)
        int[,] edges = {
            {0,1},{1,3},{3,2},{2,0},
            {4,5},{5,7},{7,6},{6,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        // draw each edge
        for (int e = 0; e < edges.GetLength(0); e++)
        {
            var a = corners[edges[e, 0]];
            var b2 = corners[edges[e, 1]];
            Debug.DrawLine(a, b2, color, duration);
        }

        Debug.Log($"[StartRoomDimensionMarker] Drew Start-room bounds from {worldMin} to {worldMax}");
    }
}
