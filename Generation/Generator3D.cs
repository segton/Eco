using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System.Linq;
using System;

public class Generator3D : MonoBehaviour
{
    public enum CellType
    {
        None,
        Room,
        Hallway,
        Stairs
    }

    class Room
    {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size)
        {
            bounds = new BoundsInt(location, size);
        }

        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }
    public struct StairInfo
    {
        public Vector3 Position;    // world‐space center
        public Vector3Int Direction;  // horizontal dir (+X,–X,+Z or –Z)
        public int Vertical;     // +1 = up‑ramp, –1 = down‑ramp
        public Vector3Int[] Cells;
    }
    public Dictionary<Vector3Int, Vector3Int> StairCellDirections { get; private set; }
  = new Dictionary<Vector3Int, Vector3Int>();
    // exposed for DungeonMeshBuilder
    public List<StairInfo> StairCells { get; private set; } = new List<StairInfo>();
    [SerializeField]
    Vector3Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector3Int roomMaxSize;
    [SerializeField]
    GameObject cubePrefab;
    [SerializeField]
    Material redMaterial;
    [SerializeField]
    Material blueMaterial;
    [SerializeField]
    Material greenMaterial;

    Random random;
    Grid3D<CellType> grid;
    List<Room> rooms;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;

    [Header("Seed (enter 6‑digit number for reproducibility)")]
    [Tooltip("If blank or non‑numeric, we'll hash this string to an int.")]
    public string seedString = "123456";

    // internal
    
    private int seedValue;

    
    IEnumerator Start()
    {
        // 1) parse or hash the seedString into an int
        if (!int.TryParse(seedString, out seedValue))
        {
            // simple deterministic string→int hash:
            unchecked
            {
                seedValue = 0;
                foreach (char c in seedString)
                    seedValue = seedValue * 31 + c;
            }
        }

        Debug.Log($"[Generator3D] Using seed “{seedString}” → {seedValue}");

        // 2) feed that into your Random
        random = new System.Random(seedValue);

        // 3) now the rest of your pipeline stays the same
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();

        PlaceRooms();
        Triangulate();
        CreateHallways();
        PathfindHallways();
        // 2) build the combined meshes & colliders
        var meshBuilder = FindObjectOfType<DungeonMeshBuilder>();
        if (meshBuilder != null)
            meshBuilder.Build();

        // 3) wait one frame so that all MeshColliders are properly initialized
        yield return null;

        /*// 4) bake the navmesh
        var nav = GetComponent<DungeonNavMeshBuilder>();
        if (nav != null)
            nav.BuildNavMesh();*/

        // 5) then spawn your stair‐link connectors
        var linkGen = GetComponent<StairLinkGenerator>();
        if (linkGen != null)
            linkGen.GenerateLinks();
    }
    void PlaceRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector3Int location = new Vector3Int(
                random.Next(0, size.x),
                random.Next(0, size.y),
                random.Next(0, size.z)
            );

            Vector3Int roomSize = new Vector3Int(
                random.Next(1, roomMaxSize.x + 1),
                random.Next(1, roomMaxSize.y + 1),
                random.Next(1, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), roomSize + new Vector3Int(2, 0, 2));

            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                PlaceRoom(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay3D.Triangulate(vertices);
    }

    void CreateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> minimumSpanningTree = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(minimumSpanningTree);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges)
        {
            if (random.NextDouble() < 0.125)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        StairCells.Clear();      // ← reset
        StairCellDirections.Clear();
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector3Int((int)startPosf.x, (int)startPosf.y, (int)startPosf.z);
            var endPos = new Vector3Int((int)endPosf.x, (int)endPosf.y, (int)endPosf.z);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) => {
                var pathCost = new DungeonPathfinder3D.PathCost();

                var delta = b.Position - a.Position;

                if (delta.y == 0)
                {
                    //flat hallway
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    //heuristic

                    if (grid[b.Position] == CellType.Stairs)
                    {
                        return pathCost;
                    }
                    else if (grid[b.Position] == CellType.Room)
                    {
                        pathCost.cost += 5;
                    }
                    else if (grid[b.Position] == CellType.None)
                    {
                        pathCost.cost += 1;
                    }

                    pathCost.traversable = true;
                }
                else
                {
                    //staircase
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) return pathCost;

                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    //base cost + heuristic

                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset))
                    {
                        return pathCost;
                    }

                    if (grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None)
                    {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;

                        if (delta.y != 0)
                        {
                            // 1) Compute horizontal and vertical offsets
                            int xDir = Mathf.Clamp(delta.x, -1, 1);
                            int zDir = Mathf.Clamp(delta.z, -1, 1);
                            Vector3Int dir = new Vector3Int(xDir, 0, zDir);
                            Vector3Int vert = new Vector3Int(0, delta.y, 0);

                            // 2) Carve the four stair cells exactly as before
                            Vector3Int a = prev + dir;
                            Vector3Int b = prev + dir * 2;
                            Vector3Int c = a + vert;
                            Vector3Int d = b + vert;

                            foreach (var s in new[] { a, b, c, d })
                            {
                                grid[s] = CellType.Stairs;
                                PlaceStairs(s);
                                StairCellDirections[s] = dir;    // ← record cell→facing
                            }
                            // 3) Build the footprint list
                            var footprint = new List<Vector3Int> { a, b, c, d };

                            // 4) Find the minimum Y among them (the “bottom” layer)
                            int bottomY = int.MaxValue;
                            foreach (var p in footprint)
                                bottomY = Mathf.Min(bottomY, p.y);

                            // 5) Collect exactly the two bottom cells
                            var bottomCells = new List<Vector3Int>();
                            foreach (var p in footprint)
                                if (p.y == bottomY)
                                    bottomCells.Add(p);

                            // 6) If we got exactly two, compute center & facing
                            if (bottomCells.Count == 2)
                            {
                                var b0 = bottomCells[0];
                                var b1 = bottomCells[1];

                                // World‐space center: midpoint + (0.5,0.5,0.5)
                                Vector3 center = new Vector3(
                                    (b0.x + b1.x) * 0.5f + 0.5f,
                                    bottomY + 0.5f,
                                    (b0.z + b1.z) * 0.5f + 0.5f
                                );

                                // Direction from first to second
                                Vector3Int facedir = new Vector3Int(
                                    Mathf.Clamp(b1.x - b0.x, -1, 1),
                                    0,
                                    Mathf.Clamp(b1.z - b0.z, -1, 1)
                                );
                                Debug.Log($"Stair #{StairCells.Count} at {center:F2} dir = {dir}");
                                Debug.DrawRay(
    center + Vector3.up * 0.1f,        // lift it slightly off the floor
    new Vector3(dir.x, 0, dir.z) * 0.5f,
    Color.red,
    5f
);
                                // after you compute `dir` and `center`…
                                int yStep = delta.y > 0 ? 1 : -1;

                                // record both horizontal dir and vertical sign
                                StairCells.Add(new StairInfo
                                {
                                    Position = center,
                                    Direction = dir,
                                    Vertical = yStep,
                                    Cells = new[] { a, b, c, d }
                                });
                            }
                        }



                        Debug.DrawLine(prev + new Vector3(0.5f, 0.5f, 0.5f), current + new Vector3(0.5f, 0.5f, 0.5f), Color.blue, 100, false);
                    }
                }

                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.Hallway)
                    {
                        PlaceHallway(pos);
                    }
                }
            }
        }
    }

    void PlaceCube(Vector3Int location, Vector3Int size, Material material)
    {
        GameObject go = Instantiate(cubePrefab, location, Quaternion.identity);
        go.GetComponent<Transform>().localScale = size;
        go.GetComponent<MeshRenderer>().material = material;
    }

    void PlaceRoom(Vector3Int location, Vector3Int size)
    {
        PlaceCube(location, size, redMaterial);
    }

    void PlaceHallway(Vector3Int location)
    {
        PlaceCube(location, new Vector3Int(1, 1, 1), blueMaterial);
    }

    void PlaceStairs(Vector3Int location)
    {
        PlaceCube(location, new Vector3Int(1, 1, 1), greenMaterial);
    }
    public Grid3D<CellType> Grid => grid;

    // Overall dungeon dimensions (same as the serialized “size” field)
    public Vector3Int DungeonSize
   {
       get => size;
       set => size = value;
   }
    public IReadOnlyList<BoundsInt> Rooms
    => rooms.Select(r => r.bounds).ToList().AsReadOnly();
}