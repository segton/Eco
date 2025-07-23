using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Center.NetcodeForGameObjectsExample.DistributedAuthority;
using UnityEngine;
using Random = System.Random;

[RequireComponent(typeof(Transform))]
public class DecorationPlacer : MonoBehaviour
{
    [Header("Dungeon Generator (must be set)")]
    public Generator3D generator;
    [Header("Mesh Builder (for cellSize)")]
    public DungeonMeshBuilder meshBuilder;
    private float _lightChance;
    private DungeonSettingsUI _ui;
    [Header("Decoration Prefabs")]
    public GameObject torchPrefab;
    public GameObject lightPrefab;
    public GameObject cupboardPrefab;
    public GameObject cupboardyPrefab;
    public GameObject closetPrefab;
    public GameObject solidDoorPrefab;
    public GameObject pipePrefab;

    [Header("Spawn Chances (0-1)")]
    [Range(0, 1)] public float torchChance = 0.1f;
    [Range(0, 1)] public float lightChance = 0.05f;
    [Range(0, 1)] public float cupboardChance = 0.02f;
    [Range(0, 1)] public float cupboardyChance = 0.02f;
    [Range(0, 1)] public float closetChance = 0.01f;
    [Range(0, 1)] public float pipeClusterChance = 0.05f;

    [Header("Furniture Prefabs")]
    public GameObject tablePrefab;
    public GameObject chairPrefab;

    [Header("Furniture Spawn Chances (0–1)")]

    [Range(0, 1)] public float tableChance = 0.02f;
    [Range(0, 1)] public float chairChance = 0.05f;
    [Header("Pipe Cluster Settings")]
    public int minPipeCluster = 2;
    public int maxPipeCluster = 5;
    // clusterRadius now fixed to 1 cell for near-touching clumps
    private int pipeClusterRadius = 1;

    [Header("Solid Door Settings")]
    public int minDoorSeparation = 1;

    private float cellSize;
    private Vector3Int[] dirs =
    {
        Vector3Int.right, Vector3Int.left,
        new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
    };

    private Random rnd;

    void Awake()
    {
        meshBuilder ??= FindObjectOfType<DungeonMeshBuilder>();
        cellSize = meshBuilder?.cellSize ?? 1f;

        // seed RNG exactly once from host–applied seedString
        if (!int.TryParse(generator.seedString, out int seedVal))
        {
            unchecked { seedVal = 0; foreach (char c in generator.seedString) seedVal = seedVal * 31 + c; }
        }
        rnd = new System.Random(seedVal);

        // grab the light chance that the host wrote into the generator


        // kick off your decorate coroutine as soon as possible
        StartCoroutine(DelayedPlace());
    }

    private void ReSeed()
    {
        if (!int.TryParse(generator.seedString, out int seedVal))
        {
            unchecked { seedVal = 0; foreach (char c in generator.seedString) seedVal = seedVal * 31 + c; }
        }
        rnd = new System.Random(seedVal);
    }

    /*
    private void OnDestroy()
    {
        // avoid leaking that subscription
        var ui = FindObjectOfType<DungeonSettingsUI>();
        if (ui != null)
            ui.DifficultyIndex.OnValueChanged -= OnDifficultyChanged;
    }
    
    
    private void OnDifficultyChanged(int oldDiff, int newDiff)
    {
        UpdateLightChance(newDiff);
    }
    */
    /*private void UpdateLightChance(int diff)
    {
        // remap 00.8, 50.15
        float lc = Mathf.Lerp(0.8f, 0.15f, diff / 5f);
        Debug.Log($"[DecorationPlacer] difficulty={diff}  lightChance={lc:F2}");
        // overwrite the field so the rest of your code picks it up
        lightChance = lc;
    }
    
    void Start()
    {
        //StartCoroutine(DelayedPlace());
    }

    IEnumerator DelayedPlace()
    {
        // 1) grab your generator
        var gen = FindObjectOfType<Generator3D>();
        if (gen == null)
        {
            Debug.LogError("[DecorationPlacer] no Generator3D found!");
            yield break;
        }

        // 2) wait until its Grid is non-null *and* has some room cells
        yield return new WaitUntil(() =>
            gen.Grid != null &&
            gen.Grid.AllPositions()
                   .Any(p => gen.Grid[p] == Generator3D.CellType.Room)
        );
        // 2) Grab the authoritative diff *right now*
        var ui = FindObjectOfType<DungeonSettingsUI>();
        if (ui != null)
            UpdateLightChance(ui.DifficultyIndex.Value);
        yield return new WaitUntil(() =>
           generator != null &&
           generator.Grid != null &&
           generator.Grid.AllPositions().Any(p => generator.Grid[p] == Generator3D.CellType.Room)
       );
        PlaceDecorations();
    }
    */
    private void Start()
    {
        StartCoroutine(DelayedPlace());
    }
    private IEnumerator DelayedPlace()
    {
        // wait for the dungeon to finish building
        yield return new WaitUntil(() =>
            generator != null
         && generator.Grid != null
         && generator.Grid.AllPositions().Any(p => generator.Grid[p] == Generator3D.CellType.Room)
        );



        PlaceDecorations();
    }

    public void PlaceDecorations()
    {
        if (!int.TryParse(generator.seedString, out int seedVal))
        {
            unchecked { seedVal = 0; foreach (char c in generator.seedString) seedVal = seedVal * 31 + c; }
        }
        var rnd = new System.Random(seedVal);
        var grid = generator.Grid;
        var existingDoors = FindObjectsOfType<DoorInstance>()
                              .Select(d => d.cell)
                              .ToHashSet();
              // 0) Build all (dx,dz) offsets within pipeClusterRadius
            var pipeOffsets = new List<Vector3Int>();
               for (int dx = -pipeClusterRadius; dx <= pipeClusterRadius; dx++)
                      for (int dz = -pipeClusterRadius; dz <= pipeClusterRadius; dz++)
                           if (dx * dx + dz * dz <= pipeClusterRadius * pipeClusterRadius)
            pipeOffsets.Add(new Vector3Int(dx, 0, dz));

        // 3) Room-interior furniture (tables & chairs)
        foreach (var p in grid.AllPositions().Where(p =>
                grid[p] == Generator3D.CellType.Room))
        {
            // skip if any cardinal neighbor is not a Room (i.e. too close to a wall)
            bool tooCloseToWall = dirs.Any(d =>
                !grid.InBounds(p + d) ||
                grid[p + d] != Generator3D.CellType.Room);
            if (tooCloseToWall)
                continue;

            // world-space center of the floor cell
            Vector3 worldCenterFloor = new Vector3(
                (p.x + 0.5f) * cellSize,
                p.y * cellSize,
                (p.z + 0.5f) * cellSize
            );

            // spawn a table?
            if (tablePrefab != null && rnd.NextDouble() < tableChance)
            {
                Quaternion rot = Quaternion.Euler(0, rnd.Next(0, 4) * 90f, 0);
                SpawnFurniture(tablePrefab, worldCenterFloor, rot);
                continue;  // don't also spawn a chair here
            }

            // spawn a chair?
            if (chairPrefab != null && rnd.NextDouble() < chairChance)
            {
                Quaternion rot = Quaternion.Euler(0, rnd.Next(0, 4) * 90f, 0);
                SpawnFurniture(chairPrefab, worldCenterFloor, rot);
            }
        }
        // 1) Wall-face decorations
        foreach (var cellPos in grid.AllPositions()
                              .Where(p => grid[p] == Generator3D.CellType.Room
                                       || grid[p] == Generator3D.CellType.Hallway
                                       || grid[p] == Generator3D.CellType.Stairs))
        {
            foreach (var dir in dirs)
            {
                var neighbor = cellPos + dir;
                if (!grid.InBounds(neighbor) || grid[neighbor] == Generator3D.CellType.None)
                {
                    // compute midpoint and world positions
                    Vector3 mid = ((Vector3)cellPos + (Vector3)neighbor) * 0.5f + Vector3.one * 0.5f;
                    Vector3 worldMid = mid * cellSize;      // y = (cellPos.y + 0.5) * cellSize
                    Vector3 worldBottom = worldMid;
                    worldBottom.y = cellPos.y * cellSize;  // floor alignment

                    // rotation facing room
                    Quaternion baseRot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z))
                                         * Quaternion.Euler(0, 0, 0);

                    // detect stairs proximity
                    bool nearStairs = grid[cellPos] == Generator3D.CellType.Stairs
                                     || dirs.Any(d => grid.InBounds(cellPos + d)
                                                    && grid[cellPos + d] == Generator3D.CellType.Stairs);

                    // pipe clusters (clumps near touching, radius = pipeClusterRadius)
                                      if (pipePrefab != null && rnd.NextDouble() < pipeClusterChance)
                                          {
                        int clusterSize = rnd.Next(minPipeCluster, maxPipeCluster + 1);
                        var spawned = new HashSet<Vector3Int> { cellPos };
                        SpawnPipe(cellPos, dir);
                        
                                               for (int i = 1; i < clusterSize; i++)
                                                   {
                                                       // pick a random already spawned cell to branch from
                            var baseCell = spawned.ElementAt(rnd.Next(spawned.Count));
                                                       // pick a random offset within the radius
                            var off = pipeOffsets[rnd.Next(pipeOffsets.Count)];
                            var newCell = baseCell +  off;
                            
                                                       // must be valid and not already spawned
                                                       if (!grid.InBounds(newCell) ||
                            !spawned.Add(newCell))
                                                               continue;
                            var ct = grid[newCell];
                            var np = newCell + dir;
                                                       if ((ct == Generator3D.CellType.Room ||
                            ct == Generator3D.CellType.Hallway ||
                            ct == Generator3D.CellType.Stairs)
                                                           && (!grid.InBounds(np) || grid[np] == Generator3D.CellType.None))
                                                           {
                                SpawnPipe(newCell, dir);
                                                           }
                                                   }
                                               // skip the rest of this face
                                               continue;
                                           }


                    // torch: mid-wall
                    if (!nearStairs && rnd.NextDouble() < torchChance)
                    {
                        Spawn(torchPrefab, worldMid, baseRot);
                        continue;
                    }

                    // cupboard: floor-aligned
                    if (!nearStairs && rnd.NextDouble() < cupboardChance)
                    {
                        Spawn(cupboardPrefab, worldBottom, baseRot);
                        continue;
                    }

                    if (!nearStairs && rnd.NextDouble() < cupboardyChance)
                    {
                        Spawn(cupboardyPrefab, worldBottom, baseRot);
                        continue;
                    }

                    // closet: floor-aligned
                    if (!nearStairs && rnd.NextDouble() < closetChance)
                    {
                        Spawn(closetPrefab, worldBottom, baseRot);
                        continue;
                    }

                    // solid door
                    if (solidDoorPrefab != null)
                    {
                        bool nearDoor = existingDoors.Any(dc => Vector3Int.Distance(dc, cellPos) <= minDoorSeparation);
                        if (nearDoor) continue;
                        var perpA = new Vector3Int(dir.z, 0, -dir.x);
                        var perpB = -perpA;
                        bool wallsEachSide = (!grid.InBounds(cellPos + perpA)
                                              || grid[cellPos + perpA] == Generator3D.CellType.None)
                                           && (!grid.InBounds(cellPos + perpB)
                                              || grid[cellPos + perpB] == Generator3D.CellType.None);
                        if (!wallsEachSide) continue;
                        Spawn(solidDoorPrefab, worldBottom, baseRot);
                        existingDoors.Add(cellPos);
                        continue;
                    }
                }
            }
        }

        // 2) Ceiling-mounted lights
        for (int x = 0; x < grid.Size.x; x++)
            for (int y = 0; y < grid.Size.y; y++)
                for (int z = 0; z < grid.Size.z; z++)
                {
                    var pos = new Vector3Int(x, y, z);
                    var cellType = grid[pos];
                    if (cellType != Generator3D.CellType.Room && cellType != Generator3D.CellType.Hallway)
                        continue;
                    var above = pos + Vector3Int.up;
                    if (grid.InBounds(above) && grid[above] != Generator3D.CellType.None)
                        continue;
                    if (rnd.NextDouble() < lightChance)
                    {
                        Vector3 worldPos = new Vector3(
                            (x + 0.5f) * cellSize,
                            (y + 1f - 0.05f) * cellSize,
                            (z + 0.5f) * cellSize
                        );
                        Spawn(lightPrefab, worldPos, Quaternion.Euler(90f, 0f, 0f));
                    }
                }
    }

    void SpawnPipe(Vector3Int cell, Vector3Int normal)
    {
        var np = cell + normal;
        Vector3 mid = ((Vector3)cell + (Vector3)np) * 0.5f + Vector3.one * 0.5f;
        Vector3 pos = mid * cellSize;
        pos.y = cell.y * cellSize;
        Quaternion rot = Quaternion.LookRotation(new Vector3(normal.x, 0, normal.z)) * Quaternion.Euler(0, 180f, 0);
        var go = Instantiate(pipePrefab, pos, rot, transform);
        // minimal scaling: keep prefab's original height, scale only to cell size
        go.transform.localScale = new Vector3(cellSize, cellSize, cellSize);
    }

    void Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var obj = Instantiate(prefab, pos, rot, transform);
        obj.transform.localScale *= cellSize;
    }
    void SpawnFurniture(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(prefab, pos, rot, transform);
        // preserve the prefab’s own imported scale
        go.transform.localScale = prefab.transform.localScale;
    }
}
