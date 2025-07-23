using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;  // only if you want to guard to IsOwner

public class TrainBoundaryChecker : MonoBehaviour
{
    [Tooltip("Exact name of the scene where we enforce train boundaries")]
    [SerializeField] private string startSceneName = "StartyScene";
    [Tooltip("Collider on your Train GameObject (can also be found by tag)")]
    [SerializeField] private Collider trainCollider;
    [Tooltip("Optional: fallback respawn point if you never get inside the train")]
    [SerializeField] private Transform respawnPoint;

    private Vector3 _lastSafePos;
    private bool _initialized;

    void Start()
    {
        // If you tagged your train "Train", otherwise drag in via inspector:
        if (trainCollider == null)
        {
            var trainGO = GameObject.FindWithTag("Train");
            if (trainGO != null) trainCollider = trainGO.GetComponent<Collider>();
        }

        if (respawnPoint == null)
        {
            var rp = GameObject.FindWithTag("RespawnPoint");
            if (rp != null) respawnPoint = rp.transform;
        }
    }

    void Update()
    {
        // Only enforce in the start scene
        if (SceneManager.GetActiveScene().name != startSceneName)
            return;

        // Optional: if you only want local players checked
        // if (TryGetComponent<NetworkBehaviour>(out var nb) && !nb.IsOwner) return;

        if (trainCollider == null)
            return; // nothing to check against

        var pos = transform.position;
        if (trainCollider.bounds.Contains(pos))
        {
            // still inside — update safe spot
            _lastSafePos = pos;
            _initialized = true;
        }
        else if (_initialized)
        {
            // left the train — snap back
            TeleportBack();
        }
        else if (respawnPoint != null)
        {
            // never got a safe spot yet, send to respawn
            transform.position = respawnPoint.position;
            _initialized = true;
            _lastSafePos = respawnPoint.position;
        }
    }

    private void TeleportBack()
    {
        // if you’re using a CharacterController, disable it before moving:
        if (TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
            transform.position = _lastSafePos;
            cc.enabled = true;
        }
        else
        {
            transform.position = _lastSafePos;
        }
    }
}
