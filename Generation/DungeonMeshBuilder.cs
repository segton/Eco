using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using System.Linq;
using UnityEngine.UIElements;

[RequireComponent(typeof(Transform))]
public class DungeonMeshBuilder : MonoBehaviour
{
    [Header("Generator Template (unmodified)")]
    public Generator3D generator;

    [Header("Materials")]
    public Material floorMaterial;
    public Material ceilingMaterial;
    public Material wallMaterial;

    [Header("Wall Prism (thick quad)")]
    public Mesh thickQuadMesh;      // assign a 1×1×1 cube or thin extruded quad
    [Range(0.01f, 1f)]
    public float wallThickness = 0.2f;

    [Header("Stair Asset")]
    public Mesh stairMesh;          // 1×2 footprint, 2-high
    public Material stairMaterial;

    public bool buildCeiling = true;

    int groundLayer, ceilingLayer, wallLayer;
    [Header("World-space size per cell (meters)")]
    [Min(0.1f)]
    public float cellSize = 2f;  // 2× scale; set to 3f for 3×, etc.

    void Awake()
    {
        groundLayer = LayerMask.NameToLayer("Ground");
        ceilingLayer = LayerMask.NameToLayer("Ceiling");
        wallLayer = LayerMask.NameToLayer("Wall");
    }

    public void Build()
    {
        stairMesh.RecalculateBounds();
        var grid = generator.Grid;
        var size = generator.DungeonSize;

        // --- 1) Gather floor & ceiling CombineInstances ---
        var floorCI = new List<CombineInstance>();
        var ceilCI = new List<CombineInstance>();

        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                {
                    var p = new Vector3Int(x, y, z);
                    var ct = grid[p];
                    if (ct == Generator3D.CellType.Room ||
                        ct == Generator3D.CellType.Hallway)
                    {
                        // floor quad
                        floorCI.Add(new CombineInstance
                        {
                            mesh = thickQuadMesh,
                            transform = Matrix4x4.TRS(
                                new Vector3(x + .5f, y + wallThickness * .5f, z + .5f) * cellSize,
                                Quaternion.identity,
                                new Vector3(1f, wallThickness, 1f) * cellSize
                            )
                        });

                        // ceiling quad
                        if (buildCeiling)
                        {
                            ceilCI.Add(new CombineInstance
                            {
                                mesh = thickQuadMesh,
                                transform = Matrix4x4.TRS(
                                    new Vector3(x + .5f, y + 1f - wallThickness * .5f, z + .5f) * cellSize,
                                    Quaternion.identity,
                                    new Vector3(1f, wallThickness, 1f) * cellSize
                                )
                            });
                        }
                    }
                }

        // --- 2) Walls around every occupied cell ---
        var dirs = new[] {
            new Vector3Int( 1,0, 0), new Vector3Int(-1,0, 0),
            new Vector3Int( 0,0, 1), new Vector3Int( 0,0,-1)
        };
        var rots = new[] {
            Quaternion.Euler( 0, 90, 0),
            Quaternion.Euler( 0,-90, 0),
            Quaternion.identity,
            Quaternion.Euler( 0,180, 0)
        };

        var wallCI = new List<CombineInstance>();

        // 2a) “Jar-lid” above stair runs
        foreach (var s in generator.StairCells)
        {
            int dx = Mathf.Abs(s.Direction.x) == 1 ? 2 : 1;
            int dz = Mathf.Abs(s.Direction.z) == 1 ? 2 : 1;

            var center = new Vector3(
                s.Position.x,
                s.Position.y + 1.5f - wallThickness * .5f,
                s.Position.z
            );

            ceilCI.Add(new CombineInstance
            {
                mesh = thickQuadMesh,
                transform = Matrix4x4.TRS(
                    center * cellSize,
                    Quaternion.identity,
                    new Vector3(dx, wallThickness, dz) * cellSize
                )
            });
        }

        // 2b) Straight walls
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                {
                    var p = new Vector3Int(x, y, z);
                    var ct = grid[p];
                    if (ct != Generator3D.CellType.Room &&
                        ct != Generator3D.CellType.Hallway &&
                        ct != Generator3D.CellType.Stairs)
                        continue;

                    for (int i = 0; i < dirs.Length; i++)
                    {
                        var n = dirs[i];
                        var np = p + n;
                        bool emptyOrOOB =
                            np.x < 0 || np.x >= size.x ||
                            np.y < 0 || np.y >= size.y ||
                            np.z < 0 || np.z >= size.z ||
                            grid[np] == Generator3D.CellType.None;
                        if (!emptyOrOOB) continue;

                        wallCI.Add(new CombineInstance
                        {
                            mesh = thickQuadMesh,
                            transform = Matrix4x4.TRS(
                                new Vector3(
                                    p.x + .5f + n.x * .5f,
                                    p.y + .5f,
                                    p.z + .5f + n.z * .5f
                                ) * cellSize,
                                rots[i],
                                new Vector3(1f, 1f, wallThickness) * cellSize
                            )
                        });
                    }
                }

        // 2c) Stair‐side walls
        foreach (var s in generator.StairCells)
        {
            var sideA = new Vector3Int(s.Direction.z, 0, -s.Direction.x);
            var sideB = -sideA;

            foreach (var cell in s.Cells)
            {
                if (grid[cell] != Generator3D.CellType.Stairs) continue;

                foreach (var perp in new[] { sideA, sideB })
                {
                    var np = cell + perp;
                    if (np.x >= 0 && np.x < size.x &&
                        np.y >= 0 && np.y < size.y &&
                        np.z >= 0 && np.z < size.z &&
                        grid[np] == Generator3D.CellType.Stairs)
                        continue;

                    int faceIdx = System.Array.FindIndex(dirs, d => d == perp);
                    if (faceIdx < 0) continue;

                    wallCI.Add(new CombineInstance
                    {
                        mesh = thickQuadMesh,
                        transform = Matrix4x4.TRS(
                            new Vector3(
                                cell.x + .5f + perp.x * .5f,
                                cell.y + .5f,
                                cell.z + .5f + perp.z * .5f
                            ) * cellSize,
                            rots[faceIdx],
                            new Vector3(1f, 1f, wallThickness) * cellSize
                        )
                    });
                }
            }
        }

        var floorCIsByLevel = new Dictionary<int, List<CombineInstance>>();
        foreach (var ci in floorCI)
        {
            int level = Mathf.FloorToInt(ci.transform.GetColumn(3).y / cellSize);
            if (!floorCIsByLevel.TryGetValue(level, out var list))
                floorCIsByLevel[level] = list = new List<CombineInstance>();
            list.Add(ci);
        }

        foreach (var kv in floorCIsByLevel)
        {
            int level = kv.Key;
            var cis = kv.Value;
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(cis.ToArray(), true, true);

            var go = new GameObject($"Floor_Level{level}",
                        typeof(MeshFilter),
                        typeof(MeshRenderer),
                        typeof(MeshCollider),
                        typeof(NavMeshSurface)
                     );
            go.transform.SetParent(transform, false);
            go.layer = groundLayer;
            go.isStatic = true;

            go.GetComponent<MeshFilter>().mesh = mesh;
            go.GetComponent<MeshRenderer>().material = floorMaterial;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;

            var surf = go.GetComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.Children;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = 1 << groundLayer;
            surf.overrideVoxelSize = true;
            surf.voxelSize = 0.05f;
            surf.overrideTileSize = true;
            surf.tileSize = 64;
            surf.minRegionArea = 0.1f;
            surf.buildHeightMesh = true;
        }

        // 4) combine & spawn walls
        var wallMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        wallMesh.CombineMeshes(wallCI.ToArray(), true, true);
        var wallGO = new GameObject("Walls", typeof(MeshFilter), typeof(MeshRenderer));
        wallGO.transform.SetParent(transform, false);
        wallGO.layer = wallLayer;
        wallGO.isStatic = true;

        wallGO.GetComponent<MeshFilter>().mesh = wallMesh;
        wallGO.GetComponent<MeshRenderer>().material = wallMaterial;
        var wallCol = wallGO.AddComponent<MeshCollider>();
        wallCol.sharedMesh = wallMesh;

        // … add collider so NavMeshSurface sees it, but DO NOT carve …

       

        // --- 5) Combine & spawn one Ceiling (ignored in bake) ---
        if (buildCeiling && ceilCI.Count > 0)
        {
            var ceilMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ceilMesh.CombineMeshes(ceilCI.ToArray(), true, true);

            var ceilGO = new GameObject("Ceiling",
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider),
                typeof(NavMeshModifier));
            ceilGO.transform.SetParent(transform, false);
            ceilGO.layer = ceilingLayer;
            ceilGO.isStatic = true;

            ceilGO.GetComponent<MeshFilter>().mesh = ceilMesh;
            ceilGO.GetComponent<MeshRenderer>().material = ceilingMaterial;
            var ceilCol = ceilGO.GetComponent<MeshCollider>();
            ceilCol.sharedMesh = ceilMesh;

            // exclude from navmesh bake
            var mod = ceilGO.GetComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
        }

        foreach (var s in generator.StairCells)
        {
            // a) Figure out which floor‐group to parent under
            int baseLevel = Mathf.FloorToInt(s.Position.y - 0.5f);
            var parentTF = transform.Find($"Floor_Level{baseLevel}") ?? transform;

            // b) Compute world‐space vertical center from the two bottom cells
            int minCellY = s.Cells.Min(c => c.y);
            int maxCellY = s.Cells.Max(c => c.y);
            float worldCenterY = (minCellY + maxCellY) * 0.5f;
            Vector3 worldCenter = new Vector3(s.Position.x, worldCenterY, s.Position.z);

            // c) Instantiate stair GameObject
            var stairGO = new GameObject("Stair",
                              typeof(MeshFilter),
                              typeof(MeshRenderer),
                              typeof(MeshCollider)
                          );
            stairGO.layer = groundLayer;
            stairGO.isStatic = true;

            // d) Parent under the correct floor but keep the world position
            stairGO.transform.SetParent(parentTF, true);
            stairGO.transform.localScale = Vector3.one * cellSize;

            stairGO.transform.position = worldCenter * cellSize;

            // e) Rotate so it faces the right way
            Vector3 forward = new Vector3(s.Direction.x, 0, s.Direction.z);
            if (s.Vertical < 0) forward = -forward;
            stairGO.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            // f) Assign mesh and material
            var mf = stairGO.GetComponent<MeshFilter>();
            mf.mesh = stairMesh;
            stairGO.GetComponent<MeshRenderer>().material = stairMaterial;
            stairGO.GetComponent<MeshCollider>().sharedMesh = stairMesh;
            
            // 1) compute exact bottom & top cell centers
            int minY = s.Cells.Min(c => c.y);
            int maxY = s.Cells.Max(c => c.y);

            Vector3 bottomCenter = s.Cells
                .Where(c => c.y == minY)
                .Aggregate(Vector3.zero, (acc, c) => acc + (Vector3)c)
                / s.Cells.Count(c => c.y == minY)
                + Vector3.one * 0.5f;

            Vector3 topCenter = s.Cells
                .Where(c => c.y == maxY)
                .Aggregate(Vector3.zero, (acc, c) => acc + (Vector3)c)
                / s.Cells.Count(c => c.y == maxY)
                + Vector3.one * 0.5f;
            // 2) horizontal run direction – from the Stair GameObject’s forward
            Vector3 dirH = -stairGO.transform.forward;
            dirH.y = 0;
            dirH.Normalize();

            // 3) overshoot, height offset, and drop amount
            const float overshoot = 1.17f;           // how far past the cell edge
            float yOffset = wallThickness + 0.01f; // sit just above the stairs
            float lowerBy = 0.5f;                  // sink halfway into the mesh

            // 4) compute slanted endpoints, centered on stair cells
            Vector3 footWorld = bottomCenter
                - dirH * overshoot
                + Vector3.up * (yOffset - lowerBy);

            Vector3 headWorld = topCenter
                + dirH * overshoot
                + Vector3.up * (yOffset - lowerBy);

            Debug.DrawLine(footWorld, headWorld, Color.cyan, 5f);

            // 5) spawn & parent the start/end markers under the stair
            var foot = new GameObject("StairLinkFoot").transform;
            foot.SetParent(stairGO.transform, true);
            foot.position = footWorld * cellSize;

            var head = new GameObject("StairLinkHead").transform;
            head.SetParent(stairGO.transform, true);
            head.position = headWorld * cellSize;

            // 6) create the slanted NavMeshLink at the midpoint
            var linkGO = new GameObject("StairLink", typeof(NavMeshLink));
            linkGO.transform.SetParent(stairGO.transform, false);
            linkGO.transform.position = (footWorld + headWorld) * 0.5f * cellSize;

            var link = linkGO.GetComponent<NavMeshLink>();
            link.startTransform = foot;
            link.endTransform = head;
            link.width = 1f * cellSize;
            link.bidirectional = true;
            link.area = NavMesh.GetAreaFromName("Walkable");
            link.autoUpdate = true;
            link.UpdateLink();

        }


        // 7) Finally bake each floor’s NavMeshSurface
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Floor_Level")) continue;
            var surf = child.GetComponent<NavMeshSurface>();
            surf.BuildNavMesh();
        }


    }
}
