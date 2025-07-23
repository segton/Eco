using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;


public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance;

    public NetworkVariable<int> TeamScore = new NetworkVariable<int>(0);
    public NetworkList<PlayerScoreData> IndividualScores = new NetworkList<PlayerScoreData>();

    public static event Action<int> OnTeamScoreUpdated;

    [Serializable]
    public struct PlayerScoreData : INetworkSerializable, IEquatable<PlayerScoreData>
    {
        public ulong playerId;
        public FixedString64Bytes playerName;
        public int score;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref playerId);
            serializer.SerializeValue(ref playerName);
            serializer.SerializeValue(ref score);
        }

        public bool Equals(PlayerScoreData other)
        {
            return playerId == other.playerId && playerName.Equals(other.playerName) && score == other.score;
        }

        public override int GetHashCode() => HashCode.Combine(playerId, playerName, score);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            // Register any already-connected clients (host counts as one)
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                RegisterPlayer(client.ClientId);
        }

        if (IsClient)
        {
            SceneManager.sceneLoaded += OnClientSceneLoaded;
            // immediately announce your name on every spawn
            var saved = PlayerPrefs.GetString("LocalPlayerName", $"Player{NetworkManager.Singleton.LocalClientId}");
            SubmitNameServerRpc(new FixedString64Bytes(saved));
        }
    }


    public override void OnDestroy()
    {
        base.OnDestroy();
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (IsClient)
        {
            SceneManager.sceneLoaded -= OnClientSceneLoaded;
        }
    }

    private void OnClientSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Once the game scene is loaded, send the real saved name
        if (scene.name == "Start" || scene.name == "Starty")
        {
            string savedName = PlayerPrefs.GetString("LocalPlayerName", string.Empty);
            if (!string.IsNullOrEmpty(savedName))
            {
                SubmitNameServerRpc(savedName);
                Debug.Log($"[ScoreManager] (OnSceneLoaded) sent name RPC: {savedName}");
            }
        }
    }

    private void OnClientConnected(ulong clientId) => RegisterPlayer(clientId);

    private void OnClientDisconnected(ulong clientId)
    {
        for (int i = 0; i < IndividualScores.Count; i++)
        {
            if (IndividualScores[i].playerId == clientId)
            {
                IndividualScores.RemoveAt(i);
                Debug.Log($"[ScoreManager] Removed player {clientId}");
                break;
            }
        }
    }
    private bool HasPlayer(ulong clientId)
    {
        for (int i = 0; i < IndividualScores.Count; i++)
            if (IndividualScores[i].playerId == clientId)
                return true;
        return false;
    }

    /// <summary>Returns the index of the existing row, or –1 if not found.</summary>
    private int GetPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < IndividualScores.Count; i++)
            if (IndividualScores[i].playerId == clientId)
                return i;
        return -1;
    }
    private void RegisterPlayer(ulong playerId)
    {
        // don't doubleadd
        if (HasPlayer(playerId)) return;

        // give _every_ newcomer a placeholder
        string defaultName = PlayerPrefs.GetString("LocalPlayerName", $"Player{playerId}");

        var newEntry = new PlayerScoreData
        {
            playerId = playerId,
            playerName = new FixedString64Bytes(defaultName),
            score = 0
        };
        IndividualScores.Add(newEntry);
        SortScoresDescending();
        Debug.Log($"[ScoreManager] Registered {defaultName} ({playerId})");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SubmitNameServerRpc(FixedString64Bytes name, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        int idx = GetPlayerIndex(clientId);

        if (idx >= 0)
        {
            // update existing row
            var data = IndividualScores[idx];
            data.playerName = name;
            IndividualScores[idx] = data;
        }
        else
        {
            // just in case RegisterPlayer never ran
            IndividualScores.Add(new PlayerScoreData
            {
                playerId = clientId,
                playerName = name,
                score = 0
            });
        }
        SortScoresDescending();
    }

    public void AddTeamScore(int points)
    {
        if (!IsServer) return;
        TeamScore.Value += points;
        Debug.Log($"[ScoreManager] Team Score: {TeamScore.Value}");
        OnTeamScoreUpdated?.Invoke(TeamScore.Value);
    }

    public void AddIndividualScore(ulong playerId, int points)
    {
        if (!IsServer) return;
        bool found = false;
        for (int i = 0; i < IndividualScores.Count; i++)
        {
            if (IndividualScores[i].playerId == playerId)
            {
                var data = IndividualScores[i];
                data.score += points;
                IndividualScores[i] = data;
                found = true;
                Debug.Log($"[ScoreManager] {data.playerName} new score: {data.score}");
                break;
            }
        }
        if (!found)
        {
            IndividualScores.Add(new PlayerScoreData { playerId = playerId, playerName = new FixedString64Bytes(string.Empty), score = points });
            Debug.Log($"[ScoreManager] Added {playerId} with score {points}");
        }
        SortScoresDescending();
    }

    private void SortScoresDescending()
    {
        var temp = new List<PlayerScoreData>(IndividualScores.Count);
        foreach (var entry in IndividualScores) temp.Add(entry);
        temp.Sort((a, b) => b.score.CompareTo(a.score));
        IndividualScores.Clear();
        foreach (var entry in temp) IndividualScores.Add(entry);
    }
    [ServerRpc(RequireOwnership = false)]
    public void ResetScoresServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // reset the team total
        TeamScore.Value = 0;

        // reset each individual’s score, but keep them in the list
        for (int i = 0; i < IndividualScores.Count; i++)
        {
            var entry = IndividualScores[i];
            entry.score = 0;
            IndividualScores[i] = entry;
        }
    }
}
