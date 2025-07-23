using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [Header("Materials")]
    public Material fadeMaterial;      // assign your DitherTransparency mat here
    public Material opaqueMaterial;    // assign your normal Lit mat here

    Renderer[] _renderers;

    public override void OnNetworkSpawn()
    {
        // find all of the mesh renderers on this character
        _renderers = GetComponentsInChildren<Renderer>();

        // choose the right material
        var matToUse = IsOwner ? fadeMaterial : opaqueMaterial;

        // apply it
        foreach (var r in _renderers)
            r.material = matToUse;
    }
}
