using System.Collections;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

[RequireComponent(typeof(Transform))]
public class DoorPlacer : MonoBehaviour
{
    [Header("Dungeon Generator (must be set)")]
    public Generator3D generator;

    [Header("Door Prefab")]
    public GameObject doorPrefab;

    [Header("Parent for Doors (optional)")]
    public Transform doorParent;

    float cellSize = 1f;

    void Awake()
    {
        if (doorParent == null) doorParent = transform;

        // grab the same cellSize you set on your DungeonMeshBuilder
        var builder = FindObjectOfType<DungeonMeshBuilder>();
        if (builder != null)
            cellSize = builder.cellSize;
    }

    void Start()
    {
        StartCoroutine(DelayedPlace());
    }

    IEnumerator DelayedPlace()
    {
        // wait one frame so the dungeon has built
        yield return null;
        PlaceDoors();
    }

    void PlaceDoors()
    {
        var grid = generator.Grid;

        foreach (var room in generator.Rooms)
        {
            foreach (var cell in room.allPositionsWithin)
            {
                // only check boundary cells
                bool boundary =
                    cell.x == room.xMin || cell.x == room.xMax - 1 ||
                    cell.z == room.zMin || cell.z == room.zMax - 1;
                if (!boundary) continue;

                // look for a hallway neighbour
                foreach (var off in new[]{
                    Vector3Int.right, Vector3Int.left,
                    new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
                })
                {
                    var nbr = cell + off;
                    if (!grid.InBounds(nbr)) continue;
                    if (grid[nbr] == Generator3D.CellType.Hallway)
                    {
                        // 1) compute the midpoint in *grid* units
                        Vector3 gridMid = ((Vector3)cell + (Vector3)nbr) * 0.5f
                                         + new Vector3(0.5f, 0f, 0.5f);

                        // 2) scale it into *world* units
                        Vector3 worldPos = gridMid * cellSize;

                        // 3) door rotation
                        Quaternion rot = Quaternion.LookRotation(new Vector3(off.x, 0, off.z));

                        // 4) instantiate & scale the door
                        var door = Instantiate(doorPrefab, worldPos, rot, doorParent);
                        var inst = door.AddComponent<DoorInstance>();
                        inst.cell = cell;          // the “room” cell you’re iterating over
                        door.tag = "Door";       // keeps your tag logic intact

                        door.transform.localScale *= cellSize;
                        door.tag = "Door";
                        // and give it a clear name
                        door.name = "Door";
                        // one door per boundary cell
                        goto NextCell;
                    }
                }
            NextCell:;
            }
        }
    }
}
