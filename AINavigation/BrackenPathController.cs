using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

[RequireComponent(typeof(NavMeshAgent))]
public class BrackenPathController : NetworkBehaviour
{
    private NavMeshAgent navMeshAgent;
    public Transform target;

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (!IsServer) return;
        if (target != null)
        {
            navMeshAgent.SetDestination(target.position);
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                OnTargetReached();
            }
        }
    }

    public void EnableRotation(bool enable)
    {
        navMeshAgent.updateRotation = enable;
    }

    public void SetMoveSpeed(float speed)
    {
        navMeshAgent.speed = speed;
    }

    // Existing method to set destination directly.
    public void SetDestination(Vector3 position)
    {
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(position);
    }

    /// <summary>
    /// Sets the destination by sampling a valid position on the NavMesh near the provided position.
    /// </summary>
    public void SetFurthestDestination(Vector3 position)
    {
        NavMeshHit hit;
        // Try to sample a valid position within a 2 unit radius of the given position.
        if (NavMesh.SamplePosition(position, out hit, 2.0f, NavMesh.AllAreas))
        {
            SetDestination(hit.position);
        }
        else
        {
            // Fallback to directly setting the position if no valid point is found
            SetDestination(position);
        }
    }

    private void OnTargetReached()
    {
        Debug.Log("Bracken reached destination: " + navMeshAgent.destination);
    }

    public bool HasReachedTarget()
    {
        return (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance);
    }
}
