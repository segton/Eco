using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ComputerTerminal : NetworkBehaviour
{
    [Header("Terminal Setup")]
    [SerializeField] private Transform cameraAnchor;
    [SerializeField] private GameObject uiCanvas;
    [SerializeField] private Transform screenLookAt;

    // special “nobody” value
    private const ulong k_FreeId = ulong.MaxValue;

    // start with k_FreeId, but scene-objects often come up as 0 until we reset
    public NetworkVariable<ulong> occupiedBy = new NetworkVariable<ulong>(
        k_FreeId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public Transform CameraAnchor => cameraAnchor;
    public GameObject UICanvas => uiCanvas;
    public Transform ScreenLookAt => screenLookAt;
    public bool IsOccupied => occupiedBy.Value != k_FreeId;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // force an initial “free” state on the host
            occupiedBy.Value = k_FreeId;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        if (occupiedBy.Value == k_FreeId)
            occupiedBy.Value = rpcParams.Receive.SenderClientId;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReleaseInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        if (occupiedBy.Value == rpcParams.Receive.SenderClientId)
            occupiedBy.Value = k_FreeId;
    }

}
