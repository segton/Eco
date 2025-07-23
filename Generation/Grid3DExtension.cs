using System.Collections.Generic;
using UnityEngine;

public static class Grid3DExtensions
{
    public static IEnumerable<Vector3Int> AllPositions<T>(this Grid3D<T> grid)
    {
        for (int x = 0; x < grid.Size.x; x++)
            for (int y = 0; y < grid.Size.y; y++)
                for (int z = 0; z < grid.Size.z; z++)
                    yield return new Vector3Int(x, y, z);
    }
}
