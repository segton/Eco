using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Generator3D))]
public class StairLinkGenerator : MonoBehaviour
{
    Generator3D _gen;

    void Awake()
    {
        _gen = GetComponent<Generator3D>();
    }

    // call after you bake navmesh on each floor
    public void GenerateLinks()
    {
        foreach (var s in _gen.StairCells)
        {
            var go = new GameObject("NavLink_Stair");
            go.transform.position = s.Position;
            go.transform.rotation = Quaternion.LookRotation(new Vector3(s.Direction.x, 0, s.Direction.z));

            var link = go.AddComponent<NavMeshLink>();
            // local start = bottom of the run; end = one unit up/down
            link.startPoint = Vector3.zero;
            link.endPoint = Vector3.up * s.Vertical;
            link.width = 1f;
            link.bidirectional = true;
            link.area = 0;        // default “Walkable”
            link.costModifier = -1;     // default
            link.autoUpdate = false;
        }
    }
}
