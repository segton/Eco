using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Transform))]
public class RoomSetup : MonoBehaviour
{
    [Header("References (must set)")]
    public Generator3D generator;
    public Transform doorParent;
    [Header("Indicator Settings")]
    [Tooltip("How many grid-cells away from the walls the indicator may go")]
    [SerializeField]
    private float indicatorMarginCells = 0f;
    private float worldWallThickness = 0f;

    public Room[] rooms;
    private float cellSize = 1f;

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator3D>();
        var ui = FindObjectOfType<DungeonSettingsUI>();
        if (ui != null)
        {
            ui.ConfiguredSeed.OnValueChanged += (oldVal, newVal) =>
            {
                generator.seedString = newVal.ToString();
            };
            // apply immediately if it’s already set:
            if (!string.IsNullOrEmpty(ui.ConfiguredSeed.Value.ToString()))
                generator.seedString = ui.ConfiguredSeed.Value.ToString();
        }
        // Pick up the cellSize from your mesh builder
        var builder = FindObjectOfType<DungeonMeshBuilder>();
        if (builder != null)
        {
            cellSize = builder.cellSize;                         // :contentReference[oaicite:0]{index=0}
            worldWallThickness = builder.wallThickness * cellSize;// :contentReference[oaicite:1]{index=1}

            // half-cube + half-wall in world units, then convert back to “cells”
            float halfCube = (cellSize * 0.5f) * 0.5f;
            float halfWall = worldWallThickness * 0.5f;
            indicatorMarginCells = (halfCube + halfWall) / cellSize;
        }

        if (doorParent == null)
            doorParent = transform;
    }

    void Start()
    {
        StartCoroutine(SetupRoutine());
    }

    private IEnumerator SetupRoutine()
    {
        //  wait only for the generator to finish placing rooms…
        yield return new WaitUntil(() =>
            generator != null &&
            generator.Rooms != null &&
            generator.Rooms.Count > 0
        );
        int seedValue = GetSeedValue();
        Debug.Log($"[RoomSetup] Seeding Unity RNG with {seedValue}");
        UnityEngine.Random.InitState(seedValue);
        // …then build & assign exactly once:
        BuildRooms();
        AssignDoors();
    }
    private int GetSeedValue()
    {
        // mirror Generator3D’s logic :contentReference[oaicite:1]{index=1}
        var s = generator.seedString;
        if (int.TryParse(s, out var v))
            return v;

        unchecked
        {
            int hash = 0;
            foreach (char c in s)
                hash = hash * 31 + c;
            return hash;
        }
    }
    void BuildRooms()
    {
        var boundsList = generator.Rooms;
        Debug.Log("[RoomSetup] Found " + boundsList.Count + " rooms.");

        // Determine special-room indices
        int startIndex = 0;
        Vector3 startPos = (boundsList[0].center + new Vector3(0.5f, 0f, 0.5f)) * cellSize;

        int bossIndex = 0;
        float maxDist = -1f;
        for (int i = 1; i < boundsList.Count; i++)
        {
            Vector3 worldCenter = (boundsList[i].center + new Vector3(0.5f, 0f, 0.5f)) * cellSize;
            float d = Vector3.Distance(worldCenter, startPos);
            if (d > maxDist)
            {
                maxDist = d;
                bossIndex = i;
            }
        }

        var candidates = Enumerable.Range(1, boundsList.Count - 1)
                                   .Where(i => i != bossIndex)
                                   .ToList();
        int treasureIndex = candidates[Random.Range(0, candidates.Count)];
        candidates.Remove(treasureIndex);
        int objectiveIndex = candidates[Random.Range(0, candidates.Count)];

        // Create Room objects
        rooms = new Room[boundsList.Count];
        for (int i = 0; i < boundsList.Count; i++)
        {
            var b = boundsList[i];

            Vector3 gridMin = (Vector3)b.min;       // integer cellcoords of one corner
            Vector3 gridMax = (Vector3)b.max;       // = b.min + b.size (exclusive upper corner)
            Vector3 gridCenter = (gridMin + gridMax) * 0.5f;
            Vector3 worldCenter = gridCenter * cellSize;

            var go = new GameObject("Room_" + i);
            go.transform.SetParent(transform, false);
            go.transform.position = worldCenter;

            var roomComp = go.AddComponent<Room>();
            if (i == startIndex) roomComp.roomType = RoomType.Start;
            else if (i == bossIndex) roomComp.roomType = RoomType.Boss;
            else if (i == treasureIndex) roomComp.roomType = RoomType.Treasure;
            else if (i == objectiveIndex) roomComp.roomType = RoomType.Objective;
            else roomComp.roomType = RoomType.Generic;

            rooms[i] = roomComp;
            Debug.Log("[RoomSetup] Room_" + i + " => " + roomComp.roomType + " at " + worldCenter);
                        //  generate a trigger collider around the room 
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
                        // size in world units = room size in cells × cellSize

            box.size = new Vector3(
            b.size.x * cellSize,
            b.size.y * cellSize,
            b.size.z * cellSize
                        );
            box.center = Vector3.zero;
            
                        // add our trigger handler and assign its type
            var rt = go.AddComponent<RoomTrigger>();
            rt.roomType = roomComp.roomType;
            // Visual indicator cube
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "RoomIndicator";
            indicator.layer = LayerMask.NameToLayer("EdiOnly");
            indicator.transform.SetParent(go.transform, false);

            // 1) size the cube however you like
            indicator.transform.localScale = Vector3.one * (cellSize * 0.5f);

            // 2) dead-center it inside the parent room
            indicator.transform.localPosition = Vector3.zero;
            Destroy(indicator.GetComponent<Collider>());

            var rend = indicator.GetComponent<Renderer>();
            switch (roomComp.roomType)
            {
                case RoomType.Start: rend.material.color = Color.green; break;
                case RoomType.Treasure: rend.material.color = Color.yellow; break;
                case RoomType.Boss: rend.material.color = Color.red; break;
                case RoomType.Objective: rend.material.color = Color.cyan; break;
                default: rend.material.color = Color.white; break;
            }
        }
    }

    void AssignDoors()
    {
        // grab every door you placed
        var allDoors = FindObjectsOfType<DoorInstance>();
        Debug.Log($"[RoomSetup] Found {allDoors.Length} door instances.");

        // clear out any old lists
        foreach (var room in rooms)
            room.doorPoints.Clear();

        // for each door, find its room
        for (int i = 0; i < rooms.Length; i++)
        {
            var b = generator.Rooms[i];     // the BoundsInt for room i
            var roomComp = rooms[i];

            foreach (var di in allDoors)
            {
                if (b.Contains(di.cell))
                {
                    roomComp.doorPoints.Add(di.transform);
                }
            }

            Debug.Log($"[RoomSetup] Room_{i} ({roomComp.roomType}) has {roomComp.doorPoints.Count} doors.");
        }
    }

}
