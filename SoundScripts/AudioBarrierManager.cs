using UnityEngine;

public class AudioBarrierManager : MonoBehaviour
{
    [Tooltip("Y world space height of your audio barrier")]
    public float barrierHeight = 1f;

    void Update()
    {
        AudioHeightFilter.barrierY = barrierHeight;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            new Vector3(-1000, barrierHeight, -1000),
            new Vector3(1000, barrierHeight, -1000)
        );
        Gizmos.DrawLine(
            new Vector3(-1000, barrierHeight, 1000),
            new Vector3(1000, barrierHeight, 1000)
        );
    }
}
