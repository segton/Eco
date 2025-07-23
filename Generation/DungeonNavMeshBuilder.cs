using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshSurface))]
public class DungeonNavMeshBuilder : MonoBehaviour
{
    NavMeshSurface _surface;

    void Awake()
    {
        _surface = GetComponent<NavMeshSurface>();

        _surface.collectObjects = CollectObjects.Children;

        _surface.layerMask = 1 << LayerMask.NameToLayer("Ground");

        _surface.buildHeightMesh = true;
    }

  
    public void BuildNavMesh()
    {
        var rootSurface = GetComponent<NavMeshSurface>();
        if (rootSurface != null)
            Destroy(rootSurface);
        if (_surface.navMeshData != null)
            _surface.RemoveData();

        _surface.BuildNavMesh();
    }
}
