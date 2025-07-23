using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
using System.Linq;
using Unity.AI.Navigation;

public class DungeonGenerator : NetworkBehaviour
{
    [Header("Dungeon Generation Settings")]
    public int roomCount = 10;                        // Total number of rooms; can be tied to difficulty.
    public Vector2 roomMinSize = new Vector2(4, 4);     // Minimum size for procedural rooms.
    public Vector2 roomMaxSize = new Vector2(8, 8);     // Maximum size for procedural rooms.
    public Vector2 dungeonBounds = new Vector2(50, 50); // Area within which rooms are placed.
    public int seed = 12345;                          // Seed value for reproducibility.

    [Header("Prefabs")]
    [Tooltip("Prefab for a generic procedural room that will be scaled.")]
    public GameObject proceduralRoomPrefab;
    [Tooltip("List of premade room prefabs with unique designs.")]
    public List<GameObject> premadeRoomPrefabs;
    [Range(0f, 1f)]
    [Tooltip("Probability (0 to 1) to choose a premade room over a procedural one.")]
    public float premadeRoomProbability = 0.3f;
    [Tooltip("Prefab for a corridor connecting rooms.")]
    public GameObject corridorPrefab;

    [Header("Connection Options")]
    [Tooltip("If true, each door is used only once (one-to-one connections).")]
    public bool oneToOneConnections = true;

    [Header("Parents (Scene Objects)")]
    [Tooltip("Parent object for instantiated rooms (must be a scene object, not a prefab asset).")]
    public Transform roomParent;
    [Tooltip("Parent object for instantiated corridors (must be a scene object).")]
    public Transform corridorParent;

    [Header("NavMesh")]
    [Tooltip("NavMeshSurface that covers the entire generated dungeon.")]
    public NavMeshSurface navMeshSurface;

    // Internal structure representing a room node.
    private struct RoomNode
    {
        public Rect roomRect;              // The dimensions of the room (for procedural rooms).
        public Vector3 center;             // Center position of the room.
        public GameObject roomInstance;    // The instantiated room GameObject.
        public List<Transform> availableDoors; // Doors not yet connected.
    }

    // List of all placed rooms.
    private List<RoomNode> roomNodes = new List<RoomNode>();
    // Global list of open door slots: each entry is (roomIndex, door transform)
    private List<(int roomIndex, Transform door)> openDoors = new List<(int, Transform)>();

    public override void OnNetworkSpawn()
    {
        // Generate the dungeon only on the server.
        if (!IsServer)
            return;

        GenerateDungeon();
        // Rebuild the NavMesh shortly after generation so all room objects are spawned.
        Invoke(nameof(BuildNavMesh), 0.5f);
    }

    private void GenerateDungeon()
    {
        System.Random rand = new System.Random(seed);
        roomNodes.Clear();
        openDoors.Clear();

        // 1. Create the initial room at a fixed starting position.
        RoomNode startNode = CreateRoomNode(rand, Vector3.zero, isInitial: true);
        roomNodes.Add(startNode);
        AddDoorsToOpenList(0, startNode.availableDoors);

        // 2. Iteratively add new rooms until desired room count reached or no open doors available.
        int attempts = 0;
        while (roomNodes.Count < roomCount && openDoors.Count > 0 && attempts < roomCount * 10)
        {
            attempts++;
            // Pick a random open door from the global list.
            int randomDoorIndex = rand.Next(openDoors.Count);
            var (existingRoomIndex, doorTransform) = openDoors[randomDoorIndex];

            // Create a new room near the selected door.
            RoomNode newNode = CreateRoomNode(rand, doorTransform.position, isInitial: false);
            if (newNode.roomInstance == null)
                continue;

            // For connection, select a door from the new room.
            if (newNode.availableDoors.Count == 0)
                continue;
            int newRoomDoorIndex = rand.Next(newNode.availableDoors.Count);
            Transform newDoor = newNode.availableDoors[newRoomDoorIndex];

            // --- Force alignment between doorTransform (existing room) and newDoor (new room) ---
            // Offset: move new room so that its selected door aligns with the existing door.
            Vector3 offset = doorTransform.position - newDoor.position;
            newNode.roomInstance.transform.position += offset;

            // Rotation: Force new room's door to face opposite of the existing door.
            Vector3 existingForward = doorTransform.forward;
            existingForward.y = 0; existingForward.Normalize();
            Vector3 newForward = newDoor.forward;
            newForward.y = 0; newForward.Normalize();

            // Compute the required rotation to align new door to face opposite direction.
            Quaternion desiredRotation = Quaternion.FromToRotation(newForward, -existingForward);
            // Extract only Y rotation.
            float yAngle = desiredRotation.eulerAngles.y;
            Quaternion yRotationOnly = Quaternion.Euler(0, yAngle, 0);
            newNode.roomInstance.transform.rotation = yRotationOnly;

            // --- Collision Check ---
            if (DoesRoomOverlap(newNode.roomInstance))
            {
                Destroy(newNode.roomInstance);
                continue;
            }

            // Room placement is valid.
            int newIndex = roomNodes.Count;
            roomNodes.Add(newNode);
            // If one-to-one connections are enforced, remove the used door.
            if (oneToOneConnections)
                openDoors.RemoveAt(randomDoorIndex);
            // Remove the used door from the new room.
            newNode.availableDoors.RemoveAt(newRoomDoorIndex);
            // Add the remaining new room doors to the global open door list.
            AddDoorsToOpenList(newIndex, newNode.availableDoors);
        }
    }

    // Creates and returns a RoomNode. If isInitial is true, no special alignment is done.
    private RoomNode CreateRoomNode(System.Random rand, Vector3 approxPosition, bool isInitial)
    {
        GameObject chosenPrefab = proceduralRoomPrefab;
        // For non-initial rooms, randomly select between procedural and premade rooms.
        if (!isInitial && premadeRoomPrefabs != null && premadeRoomPrefabs.Count > 0 && rand.NextDouble() < premadeRoomProbability)
        {
            int index = rand.Next(0, premadeRoomPrefabs.Count);
            chosenPrefab = premadeRoomPrefabs[index];
        }

        Vector2 size = roomMaxSize;
        // For procedural rooms, randomly determine size.
        if (chosenPrefab == proceduralRoomPrefab)
        {
            float width = Mathf.Round((float)(roomMinSize.x + rand.NextDouble() * (roomMaxSize.x - roomMinSize.x)));
            float height = Mathf.Round((float)(roomMinSize.y + rand.NextDouble() * (roomMaxSize.y - roomMinSize.y)));
            size = new Vector2(width, height);
        }

        // Instantiate the room.
        GameObject newRoom = Instantiate(chosenPrefab, approxPosition, Quaternion.identity);
        // Set the parent (ensure roomParent is a scene object, not a prefab asset).
        if (roomParent != null)
            newRoom.transform.SetParent(roomParent, true);
        // Scale procedural rooms to generated dimensions.
        if (chosenPrefab == proceduralRoomPrefab)
        {
            newRoom.transform.localScale = new Vector3(size.x, 1, size.y);
        }

        // Spawn as networked object.
        NetworkObject netObj = newRoom.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
        else
            Debug.LogWarning("[DungeonGenerator] Room prefab is missing a NetworkObject component.");

        // Gather available door points.
        List<Transform> availableDoors = new List<Transform>();
        Room roomComponent = newRoom.GetComponent<Room>();
        /*if (roomComponent != null && roomComponent.doorPoints.Count > 0)
        {
            availableDoors.AddRange(roomComponent.doorPoints);
        }
        else
        {
            Debug.LogWarning("[DungeonGenerator] Room instance does not have door points.");
        }
        */
        return new RoomNode
        {
            roomRect = new Rect(0, 0, size.x, size.y),
            center = newRoom.transform.position,
            roomInstance = newRoom,
            availableDoors = availableDoors
        };
    }

    // Adds door points from a given room node to the global open door list.
    private void AddDoorsToOpenList(int roomIndex, List<Transform> doorList)
    {
        foreach (Transform door in doorList)
        {
            openDoors.Add((roomIndex, door));
        }
    }

    // Checks whether the new room overlaps any existing room.
    private bool DoesRoomOverlap(GameObject newRoom)
    {
        Collider newRoomCollider = newRoom.GetComponent<Collider>();
        if (newRoomCollider == null)
            return false;
        foreach (RoomNode node in roomNodes)
        {
            if (node.roomInstance == null)
                continue;
            Collider existingCollider = node.roomInstance.GetComponent<Collider>();
            if (existingCollider == null)
                continue;
            if (newRoomCollider.bounds.Intersects(existingCollider.bounds))
                return true;
        }
        return false;
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
            Debug.LogWarning("[DungeonGenerator] NavMeshSurface reference is not assigned.");
        }
    }
}
