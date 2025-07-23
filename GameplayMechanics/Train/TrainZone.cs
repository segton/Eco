using UnityEngine;
using Unity.Netcode;

public class TrainZone : MonoBehaviour
{
    [SerializeField] LayerMask trainLayer;

    void Start()
    {
        // mark anything already overlapping
        var cols = Physics.OverlapBox(
            GetComponent<Collider>().bounds.center,
            GetComponent<Collider>().bounds.extents,
            transform.rotation,
            trainLayer
        );
        foreach (var col in cols)
            MarkRoot(col.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & trainLayer) == 0) return;
        MarkRoot(other.transform);
    }

    void MarkRoot(Transform t)
    {
        // Unity’s Transform.root is the topmost transform in *that* hierarchy,
        // which for a stand-alone object is itself, for children is its real parent.
        var root = t.root ?? t;

        // Mark the GameObject so it survives scene unload
        DontDestroyOnLoad(root.gameObject);

       
    }

}
