using UnityEngine;
using Unity.Netcode;
using System.Linq;

[DisallowMultipleComponent]
public class MeshBudgetManager : NetworkBehaviour
{
    [Tooltip("The Camera this manager should use. If left empty, will try to find one on this GameObject.")]
    public Camera playerCamera;

    [Tooltip("Maximum number of MeshRenderers to keep enabled each update.")]
    public int maxVisibleMeshes = 150;

    MeshRenderer[] allRenderers;
    float updateInterval = 0.5f;
    float nextUpdateTime;

    public override void OnNetworkSpawn()
    {
        // Only run this logic for the local player!
        if (!IsLocalPlayer)
        {
            enabled = false;
            return;
        }

        // Grab the camera if none assigned
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            Debug.LogWarning("MeshBudgetManager: No Camera found on local player.");

        // Cache all the renderers you want to budget
        allRenderers = FindObjectsOfType<MeshRenderer>();
    }

    void Update()
    {
        // Throttle so we’re not sorting every single frame
        if (Time.time < nextUpdateTime || allRenderers == null)
            return;

        nextUpdateTime = Time.time + updateInterval;

        // If below budget, nothing to do
        if (allRenderers.Length <= maxVisibleMeshes)
            return;

        // Sort by squared distance from this player’s camera
        var byDistance = allRenderers
            .OrderBy(r => Vector3.SqrMagnitude(r.bounds.center - playerCamera.transform.position))
            .ToArray();

        // Enable the closest N, disable the rest
        for (int i = 0; i < byDistance.Length; i++)
        {
            bool shouldBeOn = i < maxVisibleMeshes;
            if (byDistance[i].enabled != shouldBeOn)
                byDistance[i].enabled = shouldBeOn;
        }
    }
}
