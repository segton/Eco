using System.Collections.Generic;
using UnityEngine;

public class PlayerDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float detectionRadius = 20f;
    [SerializeField] private float lookAngleThreshold = 360f; // Optional
    [SerializeField] public LayerMask obstacleLayer; // New: set this to include walls/obstacles

    public float DetectionRadius { get => detectionRadius; set => detectionRadius = value; }
    public float LookAngleThreshold { get => lookAngleThreshold; set => lookAngleThreshold = value; }

    /// <summary>
    /// Returns a list of PlayerMovement objects within the detection radius.
    /// </summary>
    public List<PlayerMovement> GetPlayersWithinRadius()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
        List<PlayerMovement> players = new List<PlayerMovement>();

        foreach (var collider in hitColliders)
        {
            if (collider != null && collider.TryGetComponent(out PlayerMovement player))
            {
                players.Add(player);
            }
        }
        return players;
    }

    /// <summary>
    /// Checks if any of the detected players are looking at this object.
    /// Uses the player's camera frustum and a raycast (using obstacleLayer) to ensure there’s a clear line-of-sight.
    /// </summary>
    public bool IsAnyoneLookingAtMe(List<PlayerMovement> players)
    {
        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("No players provided for IsAnyoneLookingAtMe check.");
            return false;
        }

        Collider selfCollider = GetComponent<Collider>();
        if (selfCollider == null)
        {
            Debug.LogWarning("No collider found on object for detection.");
            return false;
        }

        foreach (var player in players)
        {
            if (player == null) continue;
            Camera playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera == null)
                continue;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(playerCamera);

            if (GeometryUtility.TestPlanesAABB(frustumPlanes, selfCollider.bounds))
            {
                // Use obstacleLayer to check for blocking obstacles
                Vector3 direction = (player.transform.position - transform.position).normalized;
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (!Physics.Raycast(transform.position, direction, distance, obstacleLayer))
                {
                    Debug.Log($"{player.name} is looking at {gameObject.name} with clear line-of-sight.");
                    return true;
                }
                else
                {
                    Debug.Log($"{player.name} sees {gameObject.name} but an obstacle blocks the view.");
                }
            }
            else
            {
                Debug.Log($"{player.name}'s camera frustum does not include {gameObject.name}.");
            }
        }
        return false;
    }
}
