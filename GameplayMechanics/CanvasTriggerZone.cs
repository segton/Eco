using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
public class CanvasTriggerZone : MonoBehaviour
{
    [Tooltip("The Canvas (or panel) you want to show/hide")]
    [SerializeField] public GameObject overlayCanvas;

    // internal
    private Collider zoneCollider;
    private NetworkObject localPlayerObj;
    private bool isPlayerInside;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (overlayCanvas == null)
            Debug.LogError($"[{nameof(CanvasTriggerZone)}] No overlayCanvas assigned on {name}");
        else
            overlayCanvas.SetActive(false);
    }

    private void Update()
    {
        // if we thought they were inside, but they're now outside the bounds  exit
        if (isPlayerInside && localPlayerObj != null && overlayCanvas.activeSelf)
        {
            Vector3 pt = localPlayerObj.transform.position;
            Vector3 closest = zoneCollider.ClosestPoint(pt);
            if (Vector3.Distance(closest, pt) > 0.01f)
                ExitZone();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // look up NetworkObject on self or any parent
        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null && netObj.IsLocalPlayer)
        {
            localPlayerObj = netObj;
            EnterZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null && netObj.IsLocalPlayer)
        {
            ExitZone();
        }
    }

    private void EnterZone()
    {
        isPlayerInside = true;
        overlayCanvas.SetActive(true);
    }

    private void ExitZone()
    {
        isPlayerInside = false;
        localPlayerObj = null;
        overlayCanvas.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (zoneCollider != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            if (zoneCollider is BoxCollider b)
                Gizmos.DrawWireCube(b.center, b.size);
            else
                Gizmos.DrawWireSphere(zoneCollider.bounds.center, zoneCollider.bounds.extents.magnitude);
        }
    }
}
