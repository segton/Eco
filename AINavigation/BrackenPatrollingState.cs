using UnityEngine;
using UnityEngine.AI;

public class BrackenPatrollingState : State
{
    private Bracken _bracken;
    private float patrolTimer;
    private const float patrolInterval = 5f;

    public BrackenPatrollingState(Bracken bracken) : base(bracken.brackenFSM)
    {
        _bracken = bracken;
    }

    public override void Enter()
    {
        base.Enter();
        patrolTimer = patrolInterval;
        Vector3 nextDestination = GetRandomNavMeshPoint(_bracken.transform.position, 20f);
        _bracken.pathController.SetFurthestDestination(nextDestination);
        Debug.Log("Bracken entered PATROLLING state. Next destination: " + nextDestination);
    }

    public override void Update()
    {
        base.Update();
        patrolTimer -= Time.deltaTime;
        Debug.Log($"[PATROLLING] Timer: {patrolTimer:F2}");

        if (patrolTimer <= 0)
        {
            Vector3 nextDestination = GetRandomNavMeshPoint(_bracken.transform.position, 20f);
            _bracken.pathController.SetFurthestDestination(nextDestination);
            patrolTimer = patrolInterval;
            Debug.Log("Patrolling: New destination set: " + nextDestination);
        }

        if (_bracken.IsPlayerInSight())
        {
            _bracken.brackenFSM.SetCurrentState(new BrackenInterestedState(_bracken));
            Debug.Log("Player detected. Transitioning to INTERESTED state.");
        }
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log("Exiting PATROLLING state.");
    }

    private Vector3 GetRandomNavMeshPoint(Vector3 center, float range)
    {
        Vector3 randomDirection = Random.insideUnitSphere * range;
        randomDirection += center;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, range, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return center;
    }

    public override string ToString() => "BrackenPatrollingState";
}
