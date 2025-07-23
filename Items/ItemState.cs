using Unity.Netcode;
using UnityEngine;

public class ItemState : NetworkBehaviour
{
    public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
    public NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>();

    private void Update()
    {
        if (IsServer)
        {
            // Update the networked variables with the current transform.
            Position.Value = transform.position;
            Rotation.Value = transform.rotation;
        }
        else
        {
            // On clients, update the transform based on network variables.
            transform.position = Position.Value;
            transform.rotation = Rotation.Value;
        }
    }
}
