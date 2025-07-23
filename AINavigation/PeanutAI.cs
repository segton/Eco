using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
public class PeanutAI : NetworkBehaviour
{
    [Header("Movement & Kill Settings")]
    public float moveSpeed = 5f;
    public float killDistance = 1f;

    [Header("Layers")]
    public LayerMask playerLayer;    // Assign in Inspector
    public LayerMask obstacleLayer;  // Assign in Inspector

    private NavMeshAgent navMeshAgent;
    private PlayerDetector playerDetector;
    private Collider myCollider;

    void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.speed = moveSpeed;
        myCollider = GetComponent<Collider>();

        playerDetector = GetComponent<PlayerDetector>();
        if (playerDetector == null)
        {
            Debug.LogError("PlayerDetector component is missing on PeanutAI.");
        }
    }
    private readonly HashSet<ulong> _viewers = new();

    /// <summary>

    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void ReportLookingServerRpc(ulong clientId, bool isLooking)
    {
        if (isLooking) _viewers.Add(clientId);
        else _viewers.Remove(clientId);
    }

    private bool AnyoneWatching => _viewers.Count > 0;
    void Update()
    {
        // Run AI logic only on the host
        if (!IsServer) return;

        bool isPlayerLooking = IsAnyPlayerLooking();

        if (AnyoneWatching)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            navMeshAgent.isStopped = false;
            Transform nearestPlayer = GetNearestPlayer();
            if (nearestPlayer != null)
            {
                navMeshAgent.SetDestination(nearestPlayer.position);
                if (Vector3.Distance(transform.position, nearestPlayer.position) <= killDistance)
                {
                    KillPlayer(nearestPlayer);
                }
            }
        }
    }
    private bool IsPlayerAlive(PlayerMovement pm)
    {
        // if you prefer, just check pm.IsDead.Value instead of Health
        return pm != null && !pm.IsDead.Value && pm.Health.Value > 0;
    }
    private bool IsAnyPlayerLooking()
    {
        if (playerDetector == null) return false;
        List<PlayerMovement> players = playerDetector.GetPlayersWithinRadius();

        foreach (var player in players)
        {
            if (!IsPlayerAlive(player))
                continue;
            // 1) Skip dead/spectator players entirely
            if (player.Health.Value <= 0)
                continue;

            // 2) Grab their active Gameplay camera (must be a real Camera component on the player)
            Camera playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
                continue;

            // 3) Frustum cull test
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, myCollider.bounds))
                continue;

            // 4) Obstacle test
            Vector3 dir = (player.transform.position - transform.position).normalized;
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (Physics.Raycast(transform.position, dir, dist, obstacleLayer))
                continue;

            // a live, unobstructed looker = freeze us
            return true;
        }

        return false;
    }

    private Transform GetNearestPlayer()
    {
        if (playerDetector == null) return null;
        List<PlayerMovement> players = playerDetector.GetPlayersWithinRadius();
        Transform nearestPlayer = null;
        float nearestDistance = Mathf.Infinity;

        foreach (var player in players)
        {
            if (player == null) continue;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPlayer = player.transform;
            }
        }
        return nearestPlayer;
    }

    private void KillPlayer(Transform player)
    {
        Debug.Log("PeanutAI killed player: " + player.name);
        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.TakeDamageServerRpc(100); // Adjust damage as needed
            Debug.Log("AngelKillPlayer");
        }
    }
}
