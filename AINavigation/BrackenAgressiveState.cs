using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BrackenAggressiveState : State
{
    private Transform targetPlayer;
    private float attackCooldown = 2.0f;
    private float cooldownTimer = 0f;
    private Bracken _bracken;

    public BrackenAggressiveState(Bracken bracken) : base(bracken.brackenFSM)
    {
        _bracken = bracken;
    }

    public override void Enter()
    {
        base.Enter();
        _bracken.pathController.EnableRotation(true);
        _bracken.pathController.SetMoveSpeed(4.0f);

        targetPlayer = ChooseNearestPlayer();
        if (targetPlayer != null)
        {
            _bracken.pathController.target = targetPlayer;
            _bracken.pathController.SetFurthestDestination(targetPlayer.position);
        }

        Debug.Log("Bracken entered AGGRESSIVE state.");
    }

    public override void Update()
    {
        base.Update();

        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            Debug.Log($"[AGGRESSIVE] Cooldown timer: {cooldownTimer:F2}");
            return;
        }

        if (targetPlayer == null)
        {
            _bracken.brackenFSM.SetCurrentState(new BrackenPatrollingState(_bracken));
            Debug.Log("No target player. Transitioning to PATROLLING state.");
            return;
        }

        _bracken.pathController.SetFurthestDestination(targetPlayer.position);
        Debug.Log($"[AGGRESSIVE] Chasing target at {targetPlayer.position}");

        if (_bracken.pathController.HasReachedTarget())
        {
            Debug.Log("Target reached. Attacking player.");
            AttackPlayer();
            cooldownTimer = attackCooldown;
        }
    }

    private Transform ChooseNearestPlayer()
    {
        List<PlayerMovement> players = _bracken.playerDetector.GetPlayersWithinRadius();
        if (players == null || players.Count == 0)
        {
            Debug.Log("No players detected, staying in current state.");
            return null;
        }

        players.RemoveAll(player => player == null);
        if (players.Count == 0) return null;

        players.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(_bracken.transform.position, a.transform.position);
            float distanceB = Vector3.Distance(_bracken.transform.position, b.transform.position);
            return distanceA.CompareTo(distanceB);
        });

        return players[0].transform;
    }

    private void AttackPlayer()
    {
        Debug.Log("Bracken attacks the player!");
        PlayerMovement pm = targetPlayer.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.TakeDamageServerRpc(20); // Adjust damage as needed
        }
        _bracken.brackenFSM.SetCurrentState(new BrackenPatrollingState(_bracken));
        Debug.Log("Attack complete. Transitioning to PATROLLING state.");
        targetPlayer = null;
        _bracken.pathController.target = null;
    }

    public override void Exit()
    {
        base.Exit();
        targetPlayer = null;
        _bracken.pathController.target = null;
        Debug.Log("Exiting AGGRESSIVE state.");
    }

    public override string ToString() => "BrackenAggressiveState";
}
