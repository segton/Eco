using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

[RequireComponent(typeof(MeshRenderer))]
public class PosterMaterialRandomizer : MonoBehaviour
{
    [Tooltip("List of possible materials for this poster")]
    public List<Material> posterMaterials;

    private void Awake()
    {
        // grab any Generator3D in the scene
        var generator = UnityEngine.Object.FindAnyObjectByType<Generator3D>();
        if (generator == null)
        {
            Debug.LogWarning("[PosterMaterialRandomizer] No Generator3D found in scene.");
            return;
        }

        if (posterMaterials == null || posterMaterials.Count == 0)
        {
            Debug.LogWarning("[PosterMaterialRandomizer] No materials assigned.");
            return;
        }

        // turn seedString into a stable int hash
        int seedHash = 0;
        unchecked
        {
            foreach (char c in generator.seedString)
                seedHash = seedHash * 31 + c;
        }

        // mix in this poster's position so multiple posters vary
        seedHash ^= transform.position.GetHashCode();

        // deterministic RNG
        var rng = new Random(seedHash);

        // pick a material
        var chosen = posterMaterials[rng.Next(posterMaterials.Count)];

        // apply
        var mr = GetComponent<MeshRenderer>();
        mr.material = chosen;
    }
}
