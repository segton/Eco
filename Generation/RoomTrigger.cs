using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomTrigger : MonoBehaviour
{
    [Tooltip("Set this to match the RoomType of the room.")]
    public RoomType roomType;

    private void OnTriggerEnter(Collider other)
    {
        var beacon = other.GetComponentInChildren<NavigatorBeacon>();
        if (beacon != null)
            beacon.OnEnteredRoom(roomType);
    }

    private void OnTriggerExit(Collider other)
    {
        var beacon = other.GetComponentInChildren<NavigatorBeacon>();
        if (beacon != null)
            beacon.OnExitedRoom(roomType);
    }
}
