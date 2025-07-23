using Unity.Netcode;
using UnityEngine;

public class SceneItemDespawn : NetworkBehaviour
{
    public override void OnNetworkDespawn()
    {
        // This will run on **all** peers whenever this NetworkObject is despawned.
        // It will destroy the exact GameObject that was in the scene.
        Destroy(gameObject);
    }
}
