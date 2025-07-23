using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class BrackenInterestedState : State
{
    // Timer accumulates when the player is not looking.
    private float notLookTimer = 0f;
    private const float aggressiveThreshold = 4f; // Seconds required without player looking
    private const float aggressiveDistance = 6f; // Transition to aggressive only if enemy is within 10 units of the player
    private Bracken _bracken;
    private NavMeshAgent navMeshAgent;
    private Transform nearestPlayer;

    public BrackenInterestedState(Bracken bracken) : base(bracken.brackenFSM)
    {
        _bracken = bracken;
        navMeshAgent = _bracken.GetComponent<NavMeshAgent>();
    }

    public override void Enter()
    {
        base.Enter();
        navMeshAgent.enabled = true;
        navMeshAgent.speed = 2f;
        navMeshAgent.isStopped = false;
        notLookTimer = 0f;

        // Get the nearest player using the PlayerDetector.
        List<PlayerMovement> players = _bracken.playerDetector.GetPlayersWithinRadius();
        PlayerMovement nearest = GetNearestPlayer(players);
        nearestPlayer = nearest != null ? nearest.transform : null;
        Debug.Log("Bracken entered INTERESTED state. Nearest player: " + (nearestPlayer != null ? nearestPlayer.name : "None"));
    }

    public override void Update()
    {
        base.Update();

        // Always orient toward the player if available.
        if (nearestPlayer != null)
        {
            _bracken.transform.LookAt(nearestPlayer);
        }

        // Check if any player is looking at the enemy.
        List<PlayerMovement> players = _bracken.playerDetector.GetPlayersWithinRadius();
        bool playerIsLooking = _bracken.playerDetector.IsAnyoneLookingAtMe(players);

        if (playerIsLooking)
        {
            // Reset timer if the enemy is being looked at.
            notLookTimer = 0f;
            // Immediately switch to Shy state if the player is looking.
            _bracken.brackenFSM.SetCurrentState(new BrackenShyState(_bracken));
            Debug.Log("In INTERESTED: Player is looking at enemy. Transitioning to SHY state.");
            return;
        }
        else
        {
            // Accumulate time only when player is not looking.
            notLookTimer += Time.deltaTime;
            Debug.Log($"[INTERESTED] Player not looking. Timer = {notLookTimer:F2}");

            if (nearestPlayer != null)
            {
                float distanceToPlayer = Vector3.Distance(_bracken.transform.position, nearestPlayer.position);
                Debug.Log($"[INTERESTED] Distance to player: {distanceToPlayer:F2}");

                if (notLookTimer >= aggressiveThreshold && distanceToPlayer <= aggressiveDistance)
                {
                    _bracken.brackenFSM.SetCurrentState(new BrackenAggressiveState(_bracken));
                    Debug.Log("In INTERESTED: Player has not looked for 4 seconds and enemy is close. Transitioning to AGGRESSIVE state.");
                    return;
                }
            }
        }

        // Optional: While in Interested state, patrol around the player.
        if (nearestPlayer != null)
        {
            Vector3 randomPatrolPoint = GetRandomPatrolPoint(nearestPlayer.position, 5f);
            navMeshAgent.SetDestination(randomPatrolPoint);
        }
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log("Exiting INTERESTED state.");
    }

    private PlayerMovement GetNearestPlayer(List<PlayerMovement> players)
    {
        PlayerMovement nearest = null;
        float nearestDistance = Mathf.Infinity;
        foreach (var player in players)
        {
            if (player == null) continue;
            float dist = Vector3.Distance(_bracken.transform.position, player.transform.position);
            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearest = player;
            }
        }
        return nearest;
    }

    private Vector3 GetRandomPatrolPoint(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
            return hit.position;
        return center;
    }

    public override string ToString() => "BrackenInterestedState";
}
