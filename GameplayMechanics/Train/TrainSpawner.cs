using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TrainSpawner : NetworkBehaviour
{
    [Header("Train")]
    [SerializeField] private NetworkObject trainPrefab;      // blue-icon asset
    [SerializeField] private Transform trainSpawnPoint;  // world spawn

    [System.Serializable]
    public struct Piece
    {
        public string anchorName;     // name of child Transform on the train
        public NetworkObject prefab;         // blue-icon network prefab asset
        public bool destroyWithScene;
    }

    [Header("Dynamic Pieces")]
    [SerializeField] private Piece[] pieces;

    private NetworkObject _trainInstance;
    private List<NetworkObject> _spawnedPieces = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 1) Spawn the train root
        _trainInstance = Instantiate(
            trainPrefab,
            trainSpawnPoint.position,
            trainSpawnPoint.rotation
        );
        _trainInstance.Spawn(destroyWithScene: false);

        // 2) For each piece, find the anchor on the train, spawn & parent
        foreach (var piece in pieces)
        {
            if (piece.prefab == null) continue;

            // find the anchor transform on our new train instance
            var anchor = _trainInstance.transform.Find(piece.anchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"Anchor {piece.anchorName} not found on train!");
                continue;
            }

            // instantiate the networked piece at that anchor’s world pos/rot
            var inst = Instantiate(
                piece.prefab,
                anchor.position,
                anchor.rotation
            );
            inst.Spawn(destroyWithScene: piece.destroyWithScene);

            // parent it under the train so it moves with it
            inst.transform.SetParent(_trainInstance.transform, worldPositionStays: true);

            _spawnedPieces.Add(inst);
        }
    }
}
