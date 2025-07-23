using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static Unity.Cinemachine.IInputAxisOwner.AxisDescriptor;
using System;
using TMPro;

[RequireComponent(typeof(LineRenderer))]
public class NavigatorBeacon : MonoBehaviour
{
    [Header("Only show UI if holding local battery")]
    [Tooltip("BatteryItem on the local player; UI only appears when this isLocalHolder==true")]
    [SerializeField]
    private BatteryItem batteryItem;
    [Header("Room-exit check")]
    [SerializeField] private float roomCheckRadius = 0.2f;
    [Header("Destination UI")]
    [SerializeField] private GameObject destinationCanvas;
    [Header("Target Display UI")]
    [SerializeField] private TMP_Text targetRoomLabel;
    [SerializeField] private GameObject targetCanvas;
    private bool isInTargetRoom;
    [Header("Beacon Settings (continued)")]
    [Tooltip("Don't sample NavMesh hits more than this far above/below the camera")]
    [SerializeField] private float maxVerticalDelta = 1f;
    [Header("Beacon Settings")]
    [Tooltip("Which room type to point at")]
    [SerializeField] private RoomType targetRoomType = RoomType.Treasure;
    [Tooltip("How often (secs) to recalc the path")]
    [SerializeField] private float updateInterval = 0.5f;
    [Tooltip("How far to look for the NavMesh when sampling")]
    [SerializeField] private float sampleRadius = 0.3f;
    [Tooltip("Raise the line this far above the NavMesh")]
    [SerializeField] private float lineHeightOffset = 0.2f;
    // Put these two fields at the top of your class:
    [SerializeField] private float startSampleRadius = 1.4f;   // how far around your feet you’ll look
    [SerializeField] private float endSampleRadius = 5f;     // how far around the room center you’ll look
    [SerializeField] private float raycastHeight = 5f;     // how high above to start the down-ray
    [SerializeField] private float raycastDistance = 10f;    // how far down to raycast
    private LineRenderer lineRenderer;  
    private RoomSetup roomSetup;
    private bool isUpdating;
    private RoomType[] cycleTypes;

    void Awake()
    {
        if (targetCanvas != null)
            targetCanvas.SetActive(false);
        // autofind the local holder battery if none assigned
               if (batteryItem == null)
            batteryItem = FindObjectsOfType<BatteryItem>()
                                       .FirstOrDefault(b => b.isLocalHolder);
        // grab & configure the renderer
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = 0.05f;
        lineRenderer.positionCount = 0;
        if (lineRenderer.material == null)
            Debug.LogWarning("[NavigatorBeacon] No material on LineRendererassign a URP Unlit/Color mat in inspector", this);
        cycleTypes = Enum.GetValues(typeof(RoomType))
                        .Cast<RoomType>()
                        .Where(rt => rt != RoomType.Generic)
                        .ToArray();


        UpdateTargetLabel();
        // whenever *any* scene loads, reset & retry
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void UpdateTargetLabel()
    {
        if (targetRoomLabel == null) return;
        // if no dungeon loaded yet, or roomSetup missing:
        if (roomSetup == null || roomSetup.rooms == null || roomSetup.rooms.Length == 0)
        {
            targetRoomLabel.text = "No Target";
        }
        else
        {
            targetRoomLabel.text = $"Tracking: {targetRoomType}";
        }
    }
    void Update()
    {
        if (batteryItem == null || !batteryItem.isLocalHolder) return;
        // 1) cycle room type on C
        if (Input.GetKeyDown(KeyCode.C))
        {
            int idx = Array.IndexOf(cycleTypes, targetRoomType);
            idx = (idx + 1) % cycleTypes.Length;
            targetRoomType = cycleTypes[idx];
            UpdateTargetLabel();
            // immediately update the path to new target
            UpdatePath();
        }
    }
    // called by NavigationGadget when you pick up & toggle on
    public void ShowTargetCanvas()
    {
               // only show if we actually hold the battery locally
       if (batteryItem == null || !batteryItem.isLocalHolder)
                   {
            HideTargetCanvas();
                       return;
                   }

        UpdateTargetLabel();
        if (targetCanvas != null)
            targetCanvas.SetActive(true);
    }

    // called by NavigationGadget when you drop or toggle off
    public void HideTargetCanvas()
    {
        if (targetCanvas != null)
            targetCanvas.SetActive(false);
    }
    public void OnEnteredRoom(RoomType type)
    {
        if (!enabled) return;
        if (type != targetRoomType) return;
        isInTargetRoom = true;
        lineRenderer.positionCount = 0;
        ShowDestinationUI();
    }
    public void ShowDestinationUI()
    {
        if (batteryItem == null || !batteryItem.isLocalHolder)
                   {
            HideDestinationUI();
                       return;
                   }
        if (destinationCanvas != null)
            destinationCanvas.SetActive(true);
    }

    public void HideDestinationUI()
    {
        if (destinationCanvas != null)
            destinationCanvas.SetActive(false);
    }
    public void OnExitedRoom(RoomType type)
    {
        if (type != targetRoomType) return;
        isInTargetRoom = false;
        HideDestinationUI();
        // force a path redraw
        UpdatePath();
    }
    void Start()
    {
        InitializeBeacon();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (isUpdating)
            CancelInvoke(nameof(UpdatePath));
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // clear out old path invocation & line
        if (isUpdating)
        {
            CancelInvoke(nameof(UpdatePath));
            isUpdating = false;
            lineRenderer.positionCount = 0;
        }
        InitializeBeacon();
    }

    private void InitializeBeacon()
    {
        // try to find the RoomSetup in the new scene
        roomSetup = FindObjectOfType<RoomSetup>();
        if (roomSetup != null)
        {
            // wait until it has produced its rooms[]
            StartCoroutine(WaitForRoomsThenStart());
        }
    }

    private IEnumerator WaitForRoomsThenStart()
    {
        yield return new WaitUntil(() =>
            roomSetup.rooms != null && roomSetup.rooms.Length > 0
        );

        // now start drawing!
        InvokeRepeating(nameof(UpdatePath), 0f, updateInterval);
        isUpdating = true;
        Debug.Log($"[NavigatorBeacon] Rooms ready ({roomSetup.rooms.Length}), starting path updates", this);
    }
 


    private void UpdatePath()
    {
        if (isInTargetRoom)
        {
            bool stillInside = false;
            var hits = Physics.OverlapSphere(transform.position, roomCheckRadius);
            foreach (var c in hits)
            {
                var rt = c.GetComponent<RoomTrigger>();
                if (rt != null && rt.roomType == targetRoomType)
                {
                    stillInside = true;
                    break;
                }
            }
            if (!stillInside)
                OnExitedRoom(targetRoomType);
        }
        if (isInTargetRoom || roomSetup == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }
        if (roomSetup == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // 1) find the room
        var target = roomSetup.rooms.FirstOrDefault(r => r.roomType == targetRoomType);
        if (target == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // 2) raycast down from above the player to get the true floor point
        Vector3 startOrigin = transform.position + Vector3.up * raycastHeight;
        if (Physics.Raycast(startOrigin, Vector3.down, out var startHitInfo, raycastDistance))
        {
            startOrigin = startHitInfo.point + Vector3.up * 0.1f;
        }

        // 3) sample *that* point onto the NavMesh
        if (!NavMesh.SamplePosition(startOrigin, out var startHit, startSampleRadius, NavMesh.AllAreas))
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // 4) repeat for the room center
        Vector3 endOrigin = target.transform.position + Vector3.up * raycastHeight;
        if (Physics.Raycast(endOrigin, Vector3.down, out var endHitInfo, raycastDistance))
        {
            endOrigin = endHitInfo.point + Vector3.up * 0.1f;
        }
        if (!NavMesh.SamplePosition(endOrigin, out var endHit, endSampleRadius, NavMesh.AllAreas))
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // 5) calculate the path
        var path = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);
        Debug.Log($"[NavigatorBeacon] CalcPath {startHit.position:F1}->{endHit.position:F1}: ok={ok}, corners={path.corners.Length}", this);

        if (!ok || path.corners.Length < 2)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // 6) lift each point off the ground a bit
        var raised = new Vector3[path.corners.Length];
        for (int i = 0; i < path.corners.Length; i++)
        {
            raised[i] = path.corners[i] + Vector3.up * lineHeightOffset;
        }


         var smooth = SubdividePath(raised, 2);
         lineRenderer.positionCount = smooth.Length;
         lineRenderer.SetPositions(smooth);
         return;

        // 8) draw the line
        //lineRenderer.positionCount = raised.Length;
        //lineRenderer.SetPositions(raised);
    }

    /// <summary>
    /// Call this at runtime to switch target room types.
    /// </summary>
    public void SetTarget(RoomType newType)
    {
        targetRoomType = newType;
        UpdatePath();
    }

    // Example subdivision helper: 
    // inserts 'subdivisions' equally-spaced points between each pair of corners.
    private Vector3[] SubdividePath(Vector3[] corners, int subdivisions)
    {
        if (subdivisions <= 0) return corners;
        var points = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < corners.Length - 1; i++)
        {
            var a = corners[i];
            var b = corners[i + 1];
            for (int j = 0; j <= subdivisions; j++)
            {
                float t = j / (float)subdivisions;
                points.Add(Vector3.Lerp(a, b, t));
            }
        }
        return points.ToArray();
    }
}
