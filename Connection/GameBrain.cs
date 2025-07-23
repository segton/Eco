// In GameBrain.cs :contentReference[oaicite:1]{index=1}
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class GameBrain : NetworkBehaviour
{
    [Header("Quota Settings")]
    [Tooltip("Base score the team must hit in round 1")]
    [SerializeField] private int initialQuota = 10;
    [Tooltip("How quota grows after a successful round (e.g. 1.2 = +20%)")]
    [SerializeField] private float successGrowthMultiplier = 1.2f;
    [Tooltip("How quota grows if the team fails (compound interest, e.g. 2 = doubling)")]
    [SerializeField] private float failureGrowthMultiplier = 2f;
    [Tooltip("How many failures (debts) before game over")]
    [SerializeField] private int maxDebtCount = 2;
    [Tooltip("Name of your Game Over scene (add this to Build Settings)")]
    [SerializeField] private string gameOverSceneName = "GameOver";

    [Header("Respawn Settings")]
    [SerializeField] private string defaultSceneName = "DefaultScene";
    [SerializeField] private float respawnDelay = 3f;

    // Tracks whether the round has already ended
    private NetworkVariable<bool> _roundOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // **NEW**: current required quota for the upcoming round
    public NetworkVariable<int> CurrentQuota = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // **NEW**: how many times the team has failed so far
    public NetworkVariable<int> DebtCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
     // **NEW**: how many rounds the team has successfully completed
    public NetworkVariable<int> RoundsCompleted = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        // Initialize quota & debt when the server first spawns this object
        CurrentQuota.Value = initialQuota;
        DebtCount.Value = 0;
        RoundsCompleted.Value = 0;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsServer) return;
        // Reset the round flag whenever you reload your main game scene
        if (scene.name == defaultSceneName)
            _roundOver.Value = false;
    }

    void Update()
    {
        if (!IsServer || _roundOver.Value) return;

        // if any player is still alive, bail out
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = client.PlayerObject.GetComponent<PlayerMovement>();
            if (pm != null && pm.Health.Value > 0)
                return;
        }

        // everyone’s dead: end the round!
        StartCoroutine(HandleRoundEnd());
    }

    public IEnumerator HandleRoundEnd()
    {
        _roundOver.Value = true;

        // 1) Pause to show “You died” or similar
        yield return new WaitForSeconds(respawnDelay);

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            //var pm = playerObj.GetComponent<PlayerMovement>();
            var dsm = playerObj.GetComponent<DeadStateManager>();

            //pm?.ReviveRequestServerRpc();      // restore health/position server-side
            dsm?.ReviveClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { client.ClientId } }
            });
        }
        // 2) Check TeamScore vs. CurrentQuota
        int totalScore = ScoreManager.Instance.TeamScore.Value;
        bool success = totalScore >= CurrentQuota.Value;

        if (success)
        {
            DebtCount.Value = 0;
            RoundsCompleted.Value++;
            CurrentQuota.Value = Mathf.CeilToInt(CurrentQuota.Value * successGrowthMultiplier);
        }
        else
        {
            DebtCount.Value++;
            CurrentQuota.Value = Mathf.CeilToInt(CurrentQuota.Value * failureGrowthMultiplier);
        }
        

        // 5) If they’ve failed too many times, Game Over
        if (!success && DebtCount.Value > maxDebtCount)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                gameOverSceneName,
                LoadSceneMode.Single
            );
            yield break;
        }

        if (IsServer)
        {
            int nextQuota = CurrentQuota.Value;
            ShowRoundEndPopupClientRpc(
                totalScore,
                success,
                DebtCount.Value,
                nextQuota
            );
        }

        // 4) Wait for the popup to display
        // 4) Let the popup display for its configured duration
        float waitTime = RoundEndPopupManager.Instance != null
            ? RoundEndPopupManager.Instance.displayDuration
            : 3f;
        yield return new WaitForSeconds(waitTime);


        // 3) Before restarting, reset all scores
        ScoreManager.Instance.ResetScoresServerRpc();

        // 4) Reload the default scene to kick off the next round
        NetworkManager.Singleton.SceneManager.LoadScene(
            defaultSceneName,
            LoadSceneMode.Single
        );
        
    }
    [ClientRpc]
    public void ShowRoundEndPopupClientRpc(int totalScore, bool success, int debtCount, int newQuota)
    {
        //Debug.Log($"[ShowPopupRPC] Received on {NetworkManager.Singleton.IsServer ? "Host":"Client"}");
        if (RoundEndPopupManager.Instance != null)
            RoundEndPopupManager.Instance.ShowPopup(
                totalScore,
                success,
                debtCount,
                newQuota
            );
    }
}
