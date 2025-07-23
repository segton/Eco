using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;


public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    // runtime reference; each local Player prefab has its own DeadStateManager
    private DeadStateManager deadStateManager;

    public event System.Action<string, bool> OnPlayerDeadStatusChanged;

    // track everybody’s dead/alive
    readonly Dictionary<string, bool> deadStatus = new Dictionary<string, bool>();
    public bool IsDead { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // kick off our initialization coroutine
        StartCoroutine(InitializeRoutine());
    }

    IEnumerator InitializeRoutine()
    {
        // 1) Wait for Netcode to spawn your local player object
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null
            && NetworkManager.Singleton.LocalClient != null
            && NetworkManager.Singleton.LocalClient.PlayerObject != null
        );

        var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
        deadStateManager = playerObj.GetComponent<DeadStateManager>();
        if (deadStateManager == null)
            Debug.LogError("PlayerStateManager: could not find DeadStateManager on the local player prefab!");

        // 2) Wait for VivoxService to exist
        
    }

    void OnDestroy()
    {
      
    }

  
    /// <summary>
    /// Call this from your health/death logic whenever any player (including you) dies or revives.
    /// </summary>
    public void SetPlayerDeadStatus(string playerId, bool isDead)
    {
        deadStatus[playerId] = isDead;
        OnPlayerDeadStatusChanged?.Invoke(playerId, isDead);
        Debug.Log("the set player dead state method works");
        Debug.Log($"[PSM] SetPlayerDeadStatus called: playerId='{playerId}', IsDead={IsDead}, newIsDead={isDead}");
        // if it’s *you*, switch your Vivox channels via DeadStateManager
        if (playerId == Unity.Netcode.NetworkManager.Singleton.LocalClientId.ToString())
        {
            Debug.Log("the set player dead individual");
            IsDead = isDead;
            if (isDead)
            {
                _ = deadStateManager?.EnterDeadState();
                Debug.Log("the inside set player works");
            }
            else
                _ = deadStateManager?.ExitDeadState();
        }
        
    }
    
    public bool IsPlayerDead(string playerId)
        => deadStatus.TryGetValue(playerId, out var d) && d;
    public IEnumerable<KeyValuePair<string, bool>> GetAllDeadStatuses()
        => deadStatus;
}
