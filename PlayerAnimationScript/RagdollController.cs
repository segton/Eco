using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class RagdollController : NetworkBehaviour
{
    [Tooltip("Must be a prefab with NetworkObject + NetworkTransform")]
    public GameObject ragdollPrefab;

    [Tooltip("Optional: where to move the live model when dead to avoid ragdoll collisions")]
    public Transform liveModelRespawnPoint;

    // track the spawned ragdoll so we can despawn it later
    private NetworkObject _spawnedRagdoll;

    //  new fields for restoring bone transforms 
    private Animator _animator;
    private CharacterController _cc;
    private Transform[] _boneTransforms;
    private Vector3[] _boneLocalPositions;
    private Quaternion[] _boneLocalRotations;

    void Awake()
    {
        // cache components
        _animator = GetComponentInChildren<Animator>();
        _cc = GetComponent<CharacterController>();

        // snapshot every bone under the Animator
        if (_animator != null)
        {
            var bones = _animator.GetComponentsInChildren<Transform>();
            _boneTransforms = bones;
            _boneLocalPositions = new Vector3[bones.Length];
            _boneLocalRotations = new Quaternion[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                _boneLocalPositions[i] = bones[i].localPosition;
                _boneLocalRotations[i] = bones[i].localRotation;
            }
        }
    }

    /// <summary>
    /// Call this to switch into ragdoll mode.
    /// </summary>
    public void EnableRagdoll()
    {
        SpawnRagdollServerRpc();
        Debug.Log("Ragdoll spawned");
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnRagdollServerRpc(ServerRpcParams rpc = default)
    {
        if (_spawnedRagdoll != null) return;

        // 1) Instantiate & spawn the ragdoll on the host
        var go = Instantiate(ragdollPrefab, transform.position, transform.rotation);
        _spawnedRagdoll = go.GetComponent<NetworkObject>();
        if (_spawnedRagdoll == null)
        {
            Debug.LogError("Ragdoll prefab needs a NetworkObject component!");
            Destroy(go);
            return;
        }
        _spawnedRagdoll.Spawn();

        // 2) Hide the live model on everyone
        ToggleLiveModelClientRpc(false);

        // 3) Move the live model away (all clients)
        Vector3 offPos = liveModelRespawnPoint != null
            ? liveModelRespawnPoint.position
            : transform.position + Vector3.up * 10f;
        MoveLiveModelClientRpc(offPos);
    }

    /// <summary>
    /// Call this to undo the ragdoll and bring back the live model.
    /// </summary>
    public void DisableRagdoll()
    {
        DespawnRagdollServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void DespawnRagdollServerRpc(ServerRpcParams rpc = default)
    {
        // 1) Clean up the ragdoll
        if (_spawnedRagdoll != null)
        {
            _spawnedRagdoll.Despawn(true);
            _spawnedRagdoll = null;
        }

        // 2) Snap the live model back (all clients)
        Vector3 returnPos = liveModelRespawnPoint != null
            ? liveModelRespawnPoint.position
            : transform.position;
        MoveLiveModelClientRpc(returnPos);

        // 3) Restore the live model visuals & physics
        ToggleLiveModelClientRpc(true);
    }

    [ClientRpc]
    private void ToggleLiveModelClientRpc(bool visible)
    {
        // Renderers
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        // Colliders
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = visible;

        // CharacterController
        if (_cc != null)
            _cc.enabled = visible;

        // Animator
        if (_animator != null)
            _animator.enabled = visible;

        if (visible)
        {
            //  restore bone transforms to their original bind pose 
            if (_boneTransforms != null)
            {
                for (int i = 0; i < _boneTransforms.Length; i++)
                {
                    _boneTransforms[i].localPosition = _boneLocalPositions[i];
                    _boneTransforms[i].localRotation = _boneLocalRotations[i];
                }
            }

            // rebind the Animator so it overwrites all bones
            _animator?.Rebind();
            _animator?.Update(0f);

            // optionally, start in your default state (e.g. "Idle")
            _animator?.Play("Idle", 0, 0f);
            // 4) **Reset HoldArm layer weight**  
            int holdLayer = _animator.GetLayerIndex("HoldArm");
            if (holdLayer >= 0)
                _animator.SetLayerWeight(holdLayer, 0f);
        }
    }

    [ClientRpc]
    private void MoveLiveModelClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
    {
        transform.position = position;
    }
}
