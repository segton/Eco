using UnityEngine;
using UnityEngine.AI;

public class BrackenShyState : State
{
    private float shyTimer = 0f;
    private const float shyDuration = 5f; // How long the enemy stays shy
    private Bracken _bracken;
    private NavMeshAgent navMeshAgent;
    private Transform nearestPlayer;

    public BrackenShyState(Bracken bracken) : base(bracken.brackenFSM)
    {
        _bracken = bracken;
        navMeshAgent = _bracken.GetComponent<NavMeshAgent>();
    }

    public override void Enter()
    {
        base.Enter();
        shyTimer = 0f;

        // Get the nearest player
        var players = _bracken.playerDetector.GetPlayersWithinRadius();
        PlayerMovement nearest = GetNearestPlayer(players);
        nearestPlayer = nearest != null ? nearest.transform : null;

        // Set a faster move speed for escape and disable rotation (so it won't look at the player)
        navMeshAgent.speed = 3f;
        navMeshAgent.isStopped = false;
        _bracken.pathController.EnableRotation(false);

        // Set destination away from the player (if available)
        if (nearestPlayer != null)
        {
            Vector3 escapeDirection = (_bracken.transform.position - nearestPlayer.position).normalized;
            Vector3 escapeDestination = _bracken.transform.position + escapeDirection * 10f; // run 10 units away
            _bracken.pathController.SetFurthestDestination(escapeDestination);
        }

        Debug.Log("Bracken entered SHY state.");
    }

    public override void Update()
    {
        base.Update();
        shyTimer += Time.deltaTime;
        Debug.Log($"[SHY] Shy timer: {shyTimer:F2}");

        // Check if the player is looking at the enemy while shy.
        var players = _bracken.playerDetector.GetPlayersWithinRadius();
        bool playerIsLooking = _bracken.playerDetector.IsAnyoneLookingAtMe(players);

        if (playerIsLooking)
        {
            // If the player is still looking, remain in shy state (or reset timer)
            Debug.Log("In SHY: Player is looking. Continuing to hide.");
        }
        else
        {
            // If the player stops looking, transition back to Interested immediately.
            _bracken.brackenFSM.SetCurrentState(new BrackenInterestedState(_bracken));
            Debug.Log("In SHY: Player stopped looking. Transitioning to INTERESTED state.");
            return;
        }

        // After a fixed duration, regardless of whether the player is still looking,
        // transition back to Interested (to re-assess the situation).
        if (shyTimer >= shyDuration)
        {
            _bracken.brackenFSM.SetCurrentState(new BrackenInterestedState(_bracken));
            Debug.Log("In SHY: Shy duration elapsed. Transitioning to INTERESTED state.");
        }
    }

    public override void Exit()
    {
        base.Exit();
        Debug.Log("Exiting SHY state.");
    }

    private PlayerMovement GetNearestPlayer(System.Collections.Generic.List<PlayerMovement> players)
    {
        PlayerMovement nearest = null;
        float nearestDistance = Mathf.Infinity;
        foreach (var player in players)
        {
            if (player == null) continue;
            float d = Vector3.Distance(_bracken.transform.position, player.transform.position);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = player;
            }
        }
        return nearest;
    }

    public override string ToString() => "BrackenShyState";
}
