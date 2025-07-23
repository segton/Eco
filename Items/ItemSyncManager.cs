/*using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public struct SpawnRecord : INetworkSerializable, IEquatable<SpawnRecord>
{
    public ulong objectId;
    public int prefabIndex;
    public Vector3 position;
    public Quaternion rotation;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref objectId);
        s.SerializeValue(ref prefabIndex);
        s.SerializeValue(ref position);
        s.SerializeValue(ref rotation);
    }

    public bool Equals(SpawnRecord other)
    {
        return objectId == other.objectId
            && prefabIndex == other.prefabIndex
            && position == other.position
            && rotation == other.rotation;
    }
}

public struct DespawnRecord : INetworkSerializable, IEquatable<DespawnRecord>
{
    public ulong objectId;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref objectId);
    }

    public bool Equals(DespawnRecord other)
    {
        return objectId == other.objectId;
    }
}

public class ItemSyncManager : NetworkBehaviour
{
    public static ItemSyncManager Instance { get; private set; }

    private NetworkList<SpawnRecord> _spawnList;
    private NetworkList<DespawnRecord> _despawnList;

    private readonly Dictionary<ulong, GameObject> _visuals = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _spawnList = new NetworkList<SpawnRecord>(
            new List<SpawnRecord>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        _despawnList = new NetworkList<DespawnRecord>(
            new List<DespawnRecord>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _spawnList.Clear();
            _despawnList.Clear();
        }

        if (IsClient && !IsHost)
        {

            _spawnList.OnListChanged += OnSpawnListChanged;
            _despawnList.OnListChanged += OnDespawnListChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        _spawnList.OnListChanged -= OnSpawnListChanged;
        _despawnList.OnListChanged -= OnDespawnListChanged;

        foreach (var go in _visuals.Values)
        {
            Destroy(go);
        }
        _visuals.Clear();
    }

    // Server call to spawn and record
    public void RecordAndSpawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;
        if (pos == Vector3.zero) return;

        var go = Instantiate(prefab, pos, rot);
        var netObj = go.GetComponent<NetworkObject>();
        netObj.Spawn();

        var items = ItemDatabase.Instance.items;
        int idx = Array.FindIndex(items, e => e.prefab == prefab);
        if (idx < 0) idx = 0;

        _spawnList.Add(new SpawnRecord
        {
            objectId = netObj.NetworkObjectId,
            prefabIndex = idx,
            position = pos,
            rotation = rot
        });
    }

    // Server call to despawn and record
    public void RecordAndDespawn(NetworkObject netObj)
    {
        if (!IsServer) return;

        ulong id = netObj.NetworkObjectId;
        netObj.Despawn();
        _despawnList.Add(new DespawnRecord { objectId = id });
    }

    private void OnSpawnListChanged(NetworkListEvent<SpawnRecord> change)
    {
        if (change.Type != NetworkListEvent<SpawnRecord>.EventType.Add) return;
        ApplySpawn(change.Value);
    }

    private void OnDespawnListChanged(NetworkListEvent<DespawnRecord> change)
    {
        if (change.Type != NetworkListEvent<DespawnRecord>.EventType.Add) return;
        ApplyDespawn(change.Value);
    }

    private void ApplySpawn(SpawnRecord rec)
    {
        if (IsServer && NetworkManager.Singleton.IsHost) return;
        if (rec.position == Vector3.zero) return;

        var entry = ItemDatabase.Instance.items[rec.prefabIndex];
        var go = Instantiate(entry.prefab, rec.position, rec.rotation);

        var netComp = go.GetComponent<NetworkObject>();
        if (netComp != null) Destroy(netComp);
        var rbComp = go.GetComponent<NetworkRigidbody>();
        if (rbComp != null) Destroy(rbComp);

        _visuals[rec.objectId] = go;
    }

    private void ApplyDespawn(DespawnRecord rec)
    {
        if (_visuals.TryGetValue(rec.objectId, out var go))
        {
            Destroy(go);
            _visuals.Remove(rec.objectId);
        }
    }
}
*/