using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
using System.Linq;
using Unity.AI.Navigation;

public class DungeonGeneratorVazgriz : NetworkBehaviour
{
    [Header("Dungeon Settings")]
    [Tooltip("Total number of rooms in the dungeon (affects difficulty).")]
    public int roomCount = 10;
    [Tooltip("The overall area within which rooms will be placed.")]
    public Vector2 dungeonBounds = new Vector2(50, 50);
    [Tooltip("Random seed for reproducible generation.")]
    public int seed = 12345;

    [Header("Room Size Settings (for procedural rooms)")]
    public Vector2 roomMinSize = new Vector2(4, 4);
    public Vector2 roomMaxSize = new Vector2(8, 8);

    [Header("Prefabs")]
    [Tooltip("Prefab for a procedural (scalable) room.")]
    public GameObject proceduralRoomPrefab;
    [Tooltip("List of premade room prefabs with unique designs.")]
    public List<GameObject> premadeRoomPrefabs;
    [Range(0f, 1f)]
    [Tooltip("Probability to choose a premade room over a procedural room.")]
    public float premadeRoomProbability = 0.3f;
    [Tooltip("Prefab for a corridor connecting rooms.")]
    public GameObject corridorPrefab;

    [Header("Connection Options")]
    [Tooltip("Extra edge probability (for cycles), e.g., 0.125 = 12.5% chance to add an extra connection.")]
    public float extraEdgeProbability = 0.125f;

    [Header("Parents (Scene Objects)")]
    [Tooltip("Parent transform to hold all room instances (scene object).")]
    public Transform roomParent;
    [Tooltip("Parent transform to hold all corridor instances (scene object).")]
    public Transform corridorParent;

    [Header("NavMesh")]
    [Tooltip("NavMeshSurface covering the dungeon – will be rebuilt after generation.")]
    public NavMeshSurface navMeshSurface;

    // Internal data structure for room information.
    private class RoomData
    {
        public Rect rect;       // Dimensions of the room (for procedural rooms)
        public Vector2 center;  // Center in 2D space
        public GameObject instance;   // Instantiated room GameObject
    }

    // List of placed rooms.
    private List<RoomData> rooms = new List<RoomData>();
    // Graph edges: each edge connects two room indices and has a weight (distance)
    private List<(int a, int b, float distance)> graphEdges = new List<(int, int, float)>();

    public override void OnNetworkSpawn()
    {
        // Only generate on the server.
        if (!IsServer)
            return;

        GenerateDungeon();
        // Rebuild NavMesh after a short delay to allow all objects to spawn.
        Invoke(nameof(BuildNavMesh), 0.5f);
    }

    private void GenerateDungeon()
    {
        System.Random rand = new System.Random(seed);
        rooms.Clear();
        graphEdges.Clear();

        // STEP 1: Place Rooms
        int attempts = 0;
        while (rooms.Count < roomCount && attempts < roomCount * 20)
        {
            attempts++;

            // Determine room size (for procedural rooms) randomly.
            Vector2 size = new Vector2(
                Mathf.Round((float)(roomMinSize.x + rand.NextDouble() * (roomMaxSize.x - roomMinSize.x))),
                Mathf.Round((float)(roomMinSize.y + rand.NextDouble() * (roomMaxSize.y - roomMinSize.y)))
            );
            // Random position within dungeon bounds.
            float x = (float)(rand.NextDouble() * dungeonBounds.x) - dungeonBounds.x / 2;
            float y = (float)(rand.NextDouble() * dungeonBounds.y) - dungeonBounds.y / 2;
            Rect newRect = new Rect(x, y, size.x, size.y);
            // Add a 1-unit buffer.
            Rect newRectBuffered = new Rect(newRect.x - 1, newRect.y - 1, newRect.width + 2, newRect.height + 2);

            // Check for overlap with existing rooms (using buffered rectangles).
            bool overlaps = rooms.Any(r => r.rect.Overlaps(newRectBuffered));
            if (overlaps)
                continue;

            // Create RoomData.
            RoomData rd = new RoomData();
            rd.rect = newRect;
            rd.center = new Vector2(newRect.x + newRect.width / 2, newRect.y + newRect.height / 2);
            rooms.Add(rd);
        }

        // STEP 2: Instantiate Room Prefabs
        // For each room, instantiate a room based on random selection:
        for (int i = 0; i < rooms.Count; i++)
        {
            GameObject chosenPrefab = proceduralRoomPrefab;
            // Use premade room prefab with specified probability.
            if (premadeRoomPrefabs != null && premadeRoomPrefabs.Count > 0 && rand.NextDouble() < premadeRoomProbability)
            {
                int idx = rand.Next(premadeRoomPrefabs.Count);
                chosenPrefab = premadeRoomPrefabs[idx];
            }
            RoomData rd = rooms[i];
            // Use the 2D center, convert to 3D (set y = 0).
            Vector3 position = new Vector3(rd.center.x, 0, rd.center.y);
            GameObject roomInstance = Instantiate(chosenPrefab, position, Quaternion.identity);
            if (roomParent != null)
                roomInstance.transform.SetParent(roomParent, true);

            // For procedural rooms, scale to room rect dimensions.
            if (chosenPrefab == proceduralRoomPrefab)
            {
                roomInstance.transform.localScale = new Vector3(rd.rect.width, 1, rd.rect.height);
            }
            rd.instance = roomInstance;
            rooms[i] = rd;
        }

        // STEP 3: Build Connectivity Graph (complete graph by default).
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                float dist = Vector2.Distance(rooms[i].center, rooms[j].center);
                graphEdges.Add((i, j, dist));
            }
        }
        // From the complete graph, compute the Minimum Spanning Tree (MST) using Prim’s algorithm.
        List<(int a, int b)> mstEdges = ComputeMST(rooms.Count, graphEdges);
        // Step 4: Randomly add some extra edges from the complete graph.
        List<(int a, int b)> extraEdges = graphEdges.Where(e => rand.NextDouble() < extraEdgeProbability)
                                                     .Select(e => (e.a, e.b))
                                                     .ToList();

        // Combine MST and extra edges.
        List<(int a, int b)> finalEdges = new List<(int, int)>();
        finalEdges.AddRange(mstEdges);
        finalEdges.AddRange(extraEdges);

        // STEP 5: Carve Hallways for Each Edge.
        foreach (var edge in finalEdges)
        {
            CarveHallway(rooms[edge.a].center, rooms[edge.b].center);
        }
    }

    // Compute MST using Prim’s algorithm.
    private List<(int a, int b)> ComputeMST(int nRooms, List<(int a, int b, float distance)> edges)
    {
        List<(int a, int b)> result = new List<(int, int)>();
        HashSet<int> inTree = new HashSet<int>();
        inTree.Add(0);
        while (inTree.Count < nRooms)
        {
            // Find the smallest edge connecting a room in the tree to one not in the tree.
            (int a, int b, float distance) bestEdge = (-1, -1, float.MaxValue);
            foreach (var edge in edges)
            {
                bool aIn = inTree.Contains(edge.a);
                bool bIn = inTree.Contains(edge.b);
                if (aIn ^ bIn) // only one is inside the tree
                {
                    if (edge.distance < bestEdge.distance)
                        bestEdge = edge;
                }
            }
            if (bestEdge.a == -1)
                break;
            result.Add((bestEdge.a, bestEdge.b));
            inTree.Add(bestEdge.a);
            inTree.Add(bestEdge.b);
        }
        return result;
    }

    // Carves an L-shaped hallway between two room centers.
    // For simplicity, we will create two corridor segments: one horizontal, one vertical.
    private void CarveHallway(Vector2 centerA, Vector2 centerB)
    {
        // Calculate intermediate point: first move horizontally from A to x of B, then vertically to B.
        Vector2 intermediate = new Vector2(centerB.x, centerA.y);
        // Create first corridor segment.
        CreateCorridorSegment(centerA, intermediate);
        // Create second corridor segment.
        CreateCorridorSegment(intermediate, centerB);
    }

    // Creates a corridor segment between two 2D points.
    private void CreateCorridorSegment(Vector2 from2D, Vector2 to2D)
    {
        if (corridorPrefab == null)
        {
            Debug.LogWarning("[DungeonGenerator] Corridor prefab not assigned.");
            return;
        }
        Vector3 from = new Vector3(from2D.x, 0, from2D.y);
        Vector3 to = new Vector3(to2D.x, 0, to2D.y);
        Vector3 midPoint = (from + to) / 2f;
        float length = Vector3.Distance(from, to);
        // Determine rotation: align with vector (to - from) while keeping only Y rotation.
        Vector3 dir = to - from;
        dir.y = 0; dir.Normalize();
        Quaternion rotation = Quaternion.LookRotation(dir);
        // Force Y-only rotation.
        rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);

        GameObject corridor = Instantiate(corridorPrefab, midPoint, rotation);
        if (corridorParent != null)
            corridor.transform.SetParent(corridorParent, true);
        // Assume corridor prefab is 1 unit long by default along Z axis.
        corridor.transform.localScale = new Vector3(corridor.transform.localScale.x,
                                                     corridor.transform.localScale.y,
                                                     length);

        NetworkObject netObj = corridor.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
        else
            Debug.LogWarning("[DungeonGenerator] Corridor prefab is missing a NetworkObject component.");
    }

    private void BuildNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("[DungeonGenerator] NavMesh rebuilt.");
        }
        else
        {
            Debug.LogWarning("[DungeonGenerator] NavMeshSurface reference not assigned.");
        }
    }
}
