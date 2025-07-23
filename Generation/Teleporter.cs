using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Collider))]
public class Teleporter : MonoBehaviour
{
    [Tooltip("Where to send the player; if null and autoFindStartRoom=true, will pick the nearest Start room")]
    public Transform destination;

    [Tooltip("If destination is null, find the nearest RoomType.Start automatically")]
    public bool autoFindStartRoom = false;

    [Tooltip("Seconds to hold F before teleporting")]
    public float holdDuration = 2f;

    [Tooltip("Tag your Player GameObject uses")]
    public string playerTag = "Player";

    float holdCounter = 0f;

    void Reset()
    {
        // ensure we have a trigger collider
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        // lazy-find a Start room if we were configured to
        if (destination == null && autoFindStartRoom)
            FindClosestStartRoom();

        if (destination == null)
            return;

        if (Input.GetKey(KeyCode.F))
        {
            holdCounter += Time.deltaTime;
            if (holdCounter >= holdDuration)
            {
                DoTeleport(other.transform);
                holdCounter = 0f;
            }
        }
        else
        {
            // reset if they release F
            holdCounter = 0f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
            holdCounter = 0f;
    }

    void DoTeleport(Transform playerTf)
    {
        Debug.Log($"[Teleporter:{name}] Teleporting player to {destination.name}");
        var cc = playerTf.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        playerTf.position = destination.position;
        if (cc) cc.enabled = true;
    }

    void FindClosestStartRoom()
    {
        var starts = FindObjectsOfType<Room>()
                     .Where(r => r.roomType == RoomType.Start)
                     .Select(r => r.transform);
        if (!starts.Any()) return;

        destination = starts
            .OrderBy(t => (t.position - transform.position).sqrMagnitude)
            .First();

        Debug.Log($"[Teleporter:{name}] Auto-found Start room: {destination.name}");
    }
}
