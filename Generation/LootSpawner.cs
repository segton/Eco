using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class LootSpawner : NetworkBehaviour
{
    [Header("Core References (must set)")]
    public RoomSetup roomSetup;
    public Generator3D generator;
    public DungeonMeshBuilder meshBuilder;

    [Header("Loot Counts")]
    public int genericMinLoot = 2;
    public int genericMaxLoot = 4;
    public int treasureMinLoot = 5;
    public int treasureMaxLoot = 8;

    [Header("Value Buckets")]
    public int maxHighValuePerRoom = 1;
    [Range(0f, 1f)] public float highValueChance = 0.2f;
    [Range(0f, 1f)] public float midValueChance = 0.3f;

    [Header("Spawn Positioning")]
    public float cellInset = 0.1f;

    float cellSize;
    List<ItemDatabase.ItemEntry> lowValue, midValue, highValue;

    [Header("Dungeon-wide Loot Cap")]
    [Tooltip("If true, never spawn more than TotalMaxLoot items across ALL rooms.")]
    public bool enforceTotalLootLimit = false;
    [Tooltip("Maximum total items to spawn across the entire dungeon.")]
    public int totalMaxLoot = 50;

    private int totalLootSpawned = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            Debug.Log("[LootSpawner] Not server  skipping loot spawn altogether");
            return;
        }

        Debug.Log("[LootSpawner] OnNetworkSpawn on server  scheduling loot spawn");
        // grab your cellSize once
        meshBuilder ??= FindObjectOfType<DungeonMeshBuilder>();
        cellSize = meshBuilder != null ? meshBuilder.cellSize : 1f;

        // now do it
        StartCoroutine(SpawnLootCoroutine());
    }

    IEnumerator SpawnLootCoroutine()
    {
        // wait until the dungeon & rooms exist
        yield return new WaitUntil(() => roomSetup.rooms != null && roomSetup.rooms.Length > 0);
        Debug.Log("[LootSpawner] Rooms ready, bucket items");

        // bucket by value
        var all = ItemDatabase.Instance.items
           .Where(e => e.dungeonSpawnable)
           .ToList(); ;
        int minVal = all.Min(e => e.itemValue);
        int maxVal = all.Max(e => e.itemValue);
        float midThresh = minVal + (maxVal - minVal) * 0.4f;
        float highThresh = minVal + (maxVal - minVal) * 0.75f;
        lowValue = all.Where(e => e.itemValue <= midThresh).ToList();
        midValue = all.Where(e => e.itemValue > midThresh && e.itemValue <= highThresh).ToList();
        highValue = all.Where(e => e.itemValue > highThresh).ToList();

        // loop rooms exactly once
        for (int i = 0; i < roomSetup.rooms.Length; i++)
        {
            // if we've already hit the global cap, bail out
            if (enforceTotalLootLimit && totalLootSpawned >= totalMaxLoot)
                yield break;

            var roomComp = roomSetup.rooms[i];
            var b = generator.Rooms[i];

            int count = roomComp.roomType == RoomType.Treasure
                ? Random.Range(treasureMinLoot, treasureMaxLoot + 1)
                : Random.Range(genericMinLoot, genericMaxLoot + 1);

            Debug.Log($"[LootSpawner] Room {i} ({   roomComp.roomType}) spawning {count}");

            int highLeft = maxHighValuePerRoom;
            for (int j = 0; j < count; j++)
            {
                // re-check cap before each spawn
                if (enforceTotalLootLimit && totalLootSpawned >= totalMaxLoot)
                    break;

                var entry = PickEntry(ref highLeft);
                var worldPos = PickPosition(b);
                Debug.Log($"[LootSpawner]    {entry.itemName} at {worldPos}");

                var go = Instantiate(entry.prefab, worldPos, Quaternion.identity);
                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null)
                    Debug.LogError("Loot prefab needs a NetworkObject!");
                else
                    netObj.Spawn();

                totalLootSpawned++;    
                yield return null;
            }
        }

    }

    ItemDatabase.ItemEntry PickEntry(ref int highLeft)
    {
        if (highLeft > 0 && Random.value < highValueChance)
        {
            highLeft--;
            return highValue.RandomElement();
        }
        else if (Random.value < midValueChance && midValue.Count > 0)
        {
            return midValue.RandomElement();
        }
        else
        {
            return lowValue.RandomElement();
        }
    }

    Vector3 PickPosition(BoundsInt b)
    {
        float rx = Random.Range(b.xMin + cellInset, b.xMax - cellInset);
        float rz = Random.Range(b.zMin + cellInset, b.zMax - cellInset);
        float ry = b.yMin + 0.5f;
        return new Vector3(rx, ry, rz) * cellSize;
    }
}

static class ListExtensions
{
    public static T RandomElement<T>(this List<T> list)
        => list[Random.Range(0, list.Count)];
}
