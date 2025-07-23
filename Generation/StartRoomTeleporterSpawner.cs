using UnityEngine;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(RoomSetup))]
public class StartRoomTeleporterSpawner : MonoBehaviour
{
    [Header("Your RoomSetup (must set)")]
    public RoomSetup roomSetup;

    [Header("Where outside the dungeon the player appears")]
    public Transform outsideSpawnPoint;

    [Header("Hold–F settings")]
    [Tooltip("Seconds to hold F before teleporting")]
    public float holdDuration = 2f;

    [Header("Trigger sizing")]
    [Tooltip("Extra margin so the box isn’t flush against walls")]
    public Vector3 padding = new Vector3(0.1f, 0.1f, 0.1f);

    [Header("UI for Exit Zone")]
    [Tooltip("Screen-space Canvas prefab to pop up when inside the teleporter zone")]
    public GameObject overlayCanvasPrefab;


    void Start()
    {
        // Delay one frame so roomSetup.rooms[] is populated
        StartCoroutine(SpawnRoomTeleporter());
    }

    IEnumerator SpawnRoomTeleporter()
    {
        yield return null;
        yield return new WaitUntil(() => roomSetup.rooms != null && roomSetup.rooms.Length > 0);

        // find the Start room instance
        var startRoom = roomSetup.rooms.FirstOrDefault(r => r.roomType == RoomType.Start);
        if (startRoom == null)
        {
            Debug.LogError("[StartRoomTeleporterSpawner] No Start room found.");
            yield break;
        }

        // look up its BoundsInt from the generator
        int idx = System.Array.IndexOf(roomSetup.rooms, startRoom);
        var b = roomSetup.generator.Rooms[idx];

        // compute world-space min & size
        // note: grid cells are 1 unit tall in y
        float cs = roomSetup.GetComponent<DungeonMeshBuilder>().cellSize;
        Vector3 worldMin = new Vector3(b.xMin, b.yMin, b.zMin) * cs;
        Vector3 worldSize = new Vector3(b.size.x, b.size.y, b.size.z) * cs;

        // center & padded size
        Vector3 center = worldMin + worldSize * 0.5f;
        Vector3 size = worldSize + padding;

        // spawn the teleporter zone
        var zone = new GameObject("StartRoomExitZone");
        zone.transform.position = center;

        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;

        // attach your existing Teleporter script
        var tp = zone.AddComponent<Teleporter>();
        tp.destination = outsideSpawnPoint;
        tp.holdDuration = holdDuration;
        tp.autoFindStartRoom = false;   // we already know where to send them

        var canvasTrigger = zone.AddComponent<CanvasTriggerZone>();
        var overlay = Instantiate(overlayCanvasPrefab, zone.transform);
        overlay.SetActive(false);

        // assign into the trigger
        canvasTrigger.overlayCanvas = overlay;

        Debug.Log($"[StartRoomTeleporterSpawner] Spawned exit zone at {center} size={size}");
    }
}
