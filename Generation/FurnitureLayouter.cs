using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class FurnitureLayouter : MonoBehaviour
{
    [Header("Dungeon Generator")]
    public Generator3D generator;

    [Header("Furniture Prefabs")]
    [Tooltip("Must implement IFootprint on each prefab")]
    public GameObject[] furniturePrefabs;

    [Header("Parent for Furniture (optional)")]
    public Transform furnitureParent;

    [Tooltip("Fraction of each room’s floor to cover")]
    [Range(0, 1)]
    public float fillRatio = 0.2f;

    void Start()
    {
        if (furnitureParent == null) furnitureParent = transform;
        StartCoroutine(DelayedLayout());
    }

    IEnumerator DelayedLayout()
    {
        yield return null;
        LayOutFurniture();
    }

    void LayOutFurniture()
    {
        var rand = new System.Random();
        foreach (var room in generator.Rooms)
        {
            int w = room.size.x, h = room.size.z;
            bool[,] occ = new bool[w, h];

            int target = Mathf.CeilToInt(w * h * fillRatio);
            int placed = 0, attempts = 0;

            while (placed < target && attempts < target * 5)
            {
                attempts++;
                // pick random prefab
                var pfb = furniturePrefabs[rand.Next(furniturePrefabs.Length)];
                var fp = pfb.GetComponent<IFootprint>();
                if (fp == null) continue;
                var sz = fp.Size;

                int rx = rand.Next(0, w - sz.x + 1);
                int rz = rand.Next(0, h - sz.y + 1);

                // check occupancy
                bool free = true;
                for (int ix = 0; ix < sz.x && free; ix++)
                    for (int iz = 0; iz < sz.y; iz++)
                        if (occ[rx + ix, rz + iz])
                            free = false;
                if (!free) continue;

                // mark
                for (int ix = 0; ix < sz.x; ix++)
                    for (int iz = 0; iz < sz.y; iz++)
                        occ[rx + ix, rz + iz] = true;

                // world position = room min + cell pos + half footprint
                Vector3 worldPos = new Vector3(
                    room.xMin + rx + sz.x * 0.5f,
                    room.yMin + 0.5f,
                    room.zMin + rz + sz.y * 0.5f
                );
                Instantiate(pfb, worldPos, Quaternion.identity, furnitureParent);
                placed++;
            }
        }
    }
}
