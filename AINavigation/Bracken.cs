using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

[RequireComponent(typeof(BrackenPathController))]
[RequireComponent(typeof(PlayerDetector))]
public class Bracken : NetworkBehaviour
{
    public FSM brackenFSM;
    public BrackenPathController pathController;
    public PlayerDetector playerDetector;

    void Awake()
    {
        pathController = GetComponent<BrackenPathController>();
        playerDetector = GetComponent<PlayerDetector>();
    }

    void Start()
    {
        brackenFSM = new FSM();
        // Assuming BrackenFSMStateType is an enum with values for PATROLLING, INTERESTED, SHY, AGGRESSIVE
        brackenFSM.Add((int)BrackenFSMStateType.PATROLLING, new BrackenPatrollingState(this));
        brackenFSM.Add((int)BrackenFSMStateType.INTERESTED, new BrackenInterestedState(this));
        brackenFSM.Add((int)BrackenFSMStateType.SHY, new BrackenShyState(this));
        brackenFSM.Add((int)BrackenFSMStateType.AGGRESSIVE, new BrackenAggressiveState(this));

        brackenFSM.SetCurrentState(brackenFSM.GetState((int)BrackenFSMStateType.PATROLLING));
    }

    void Update()
    {
        if (!IsServer) return;
        brackenFSM.Update();
    }

    void FixedUpdate()
    {
        if (!IsServer) return;
        brackenFSM.FixedUpdate();
    }

    public bool IsPlayerInSight()
    {
        List<PlayerMovement> players = playerDetector.GetPlayersWithinRadius();
        Debug.Log($"Bracken: Found {players.Count} players within detection radius.");

        // Define a custom detection bounds centered at the enemy.
        // Adjust the size as needed – here we use a 3x3x3 box.
        Bounds detectionBounds = new Bounds(transform.position, Vector3.one * 3f);

        foreach (var player in players)
        {
            if (player == null) continue;

            Camera playerCam = player.GetComponentInChildren<Camera>();
            if (playerCam == null)
            {
                Debug.Log($"Bracken: Player {player.name} has no camera.");
                continue;
            }

            // Calculate the player's camera frustum.
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCam);
            bool inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, detectionBounds);
            Debug.Log($"Bracken: Testing player {player.name} - inFrustum: {inFrustum}");

            if (inFrustum)
            {
                // Check for obstacles using the obstacle layer from PlayerDetector.
                Vector3 direction = (player.transform.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (!Physics.Raycast(transform.position, direction, distance, playerDetector.obstacleLayer))
                {
                    Debug.Log($"Bracken: Player {player.name} has a clear line-of-sight to the enemy.");
                    return true;
                }
                else
                {
                    Debug.Log($"Bracken: Raycast blocked for player {player.name}.");
                }
            }
            else
            {
                Debug.Log($"Bracken: Player {player.name}'s camera frustum does not include the enemy.");
            }
        }

        return false;
    }



}
