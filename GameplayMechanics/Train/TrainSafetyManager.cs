using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;

public class TrainSafetyManager : NetworkBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshProUGUI element to show countdown")]
    [SerializeField] private TMP_Text timerText;

    [Header("Departure Settings")]
    [Tooltip("Collider defining the 'train' area — players inside survive departure.")]
    [SerializeField] private Collider trainAreaCollider;
    [Tooltip("Seconds after scene load until the train departs.")]
    [SerializeField] private float departureTimeout = 300f;
    [Tooltip("Seconds to wait before reviving dead players.")]
    [SerializeField] private float reviveDelay = 3f;
    [Header("Scene Names")]
    [Tooltip("All play/dungeon scenes where departure logic should run.")]
    [SerializeField] private List<string> playSceneNames = new List<string>();

    [Tooltip("Name of the scene to load after departure (if no GameBrain).")]
    [SerializeField] private string postDepartureScene = "Starty";

    // tracks when we began counting
    private float startTime;
    // whether the timer UI should be updating
    private bool countdownActive;
    // serveronly coroutine reference
    private Coroutine departureCoroutine;

    /// <summary>Seconds since the departure countdown began.</summary>
    public float ElapsedTime => Time.time - startTime;

    private void Awake()
    {
        // ensure we have the train collider
        if (trainAreaCollider == null)
        {
            var train = GameObject.FindWithTag("Train");
            if (train != null) trainAreaCollider = train.GetComponent<Collider>();
            else Debug.LogWarning("TrainSafetyManager: no GameObject tagged 'Train' found.");
        }

        // hide UI initially
        if (timerText != null)
            timerText.gameObject.SetActive(false);

        // listen to Unity scene load/unload for UI on all clients
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
        SceneManager.sceneUnloaded += OnUnitySceneUnloaded;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        // server listens for networked PlayScene load to start departure logic
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnNetcodeSceneLoad;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (playSceneNames.Contains(scene.name))
        {
            // show & start UI on all clients
            startTime = Time.time;
            countdownActive = true;
            if (timerText != null)
                timerText.gameObject.SetActive(true);
        }
    }

    private void OnUnitySceneUnloaded(Scene scene)
    {
        if (playSceneNames.Contains(scene.name))
        {
            // hide UI on all clients
            countdownActive = false;
            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }
    }

    private void OnNetcodeSceneLoad(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // only the server reacts to network load for departure logic
        if (clientId != NetworkManager.ServerClientId || !playSceneNames.Contains(sceneName))
            return;

        // start the departure countdown
        departureCoroutine = StartCoroutine(DepartureCountdown());
    }

    private IEnumerator DepartureCountdown()
    {
        // wait until departure time
        yield return new WaitForSeconds(departureTimeout);

        // “kill” players outside the train
        var deadClients = new List<ulong>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = client.PlayerObject?.GetComponent<PlayerMovement>();
            if (pm == null) continue;

            bool inside = trainAreaCollider != null
                       && trainAreaCollider.bounds.Contains(pm.transform.position);
            if (!inside)
            {
                deadClients.Add(client.ClientId);
                pm.Health.Value = 0;
            }
        }

        // wait, then revive them
        yield return new WaitForSeconds(reviveDelay);

        foreach (var clientId in deadClients)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var c))
            {
                var dsm = c.PlayerObject?.GetComponent<DeadStateManager>();
                dsm?.ReviveClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }
        }

        // hand off to GameBrain or fallback to direct load
        var brain = FindObjectOfType<GameBrain>();
        if (brain != null && brain.IsServer)
        {
            brain.StartCoroutine(brain.HandleRoundEnd());
        }
        else
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                postDepartureScene,
                LoadSceneMode.Single
            );
        }
    }

    private void Update()
    {
        // update the on-screen timer on all instances
        if (!countdownActive || timerText == null) return;

        float remaining = Mathf.Max(0f, departureTimeout - ElapsedTime);
        int m = Mathf.FloorToInt(remaining / 60f);
        int s = Mathf.FloorToInt(remaining % 60f);
        timerText.text = $"{m:00}:{s:00}";
    }

    private void OnDestroy()
    {
        // clean up subscriptions
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        SceneManager.sceneUnloaded -= OnUnitySceneUnloaded;

        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnNetcodeSceneLoad;
    }
    [ServerRpc(RequireOwnership = false)]
    public void EndRoundEarlyServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        // cancel the normal countdown if it’s running
        if (departureCoroutine != null)
            StopCoroutine(departureCoroutine);
        // start the immediate execution
        StartCoroutine(ExecuteDepartureRoutine());
    }

    // does the kill/respawn/sceneload right away (skips departureTimeout)
    private IEnumerator ExecuteDepartureRoutine()
    {
        // 1) kill outside players
        var deadClients = new List<ulong>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = client.PlayerObject?.GetComponent<PlayerMovement>();
            if (pm == null) continue;
            bool inside = trainAreaCollider != null
                       && trainAreaCollider.bounds.Contains(pm.transform.position);
            if (!inside)
            {
                deadClients.Add(client.ClientId);
                pm.Health.Value = 0;
            }
        }

        // 2) wait reviveDelay
        yield return new WaitForSeconds(reviveDelay);

        // 3) revive them
        foreach (var clientId in deadClients)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var c))
            {
                var dsm = c.PlayerObject?.GetComponent<DeadStateManager>();
                dsm?.ReviveClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }
        }

        // 4) load postdeparture scene (via GameBrain or fallback)
        var brain = FindObjectOfType<GameBrain>();
        if (brain != null && brain.IsServer)
        {
            StartCoroutine(brain.HandleRoundEnd());
        }
        else
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                postDepartureScene, LoadSceneMode.Single
            );
        }
    }
}
