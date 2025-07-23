using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

public class ScoreboardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The Content RectTransform under your ScrollView.")]
    [SerializeField] private Transform contentPanel;

    [Tooltip("Prefab with a ScoreEntry component on its root.")]
    [SerializeField] private GameObject scoreEntryPrefab;

    [Tooltip("Optional: a TMP_Text for showing total team score.")]
    [SerializeField] private TMP_Text teamScoreText;

    private void Start()
    {
        if (ScoreManager.Instance == null)
        {
            Debug.LogError("[ScoreboardUI] No ScoreManager in scene!");
            return;
        }

        // Subscribe
        ScoreManager.Instance.TeamScore.OnValueChanged += OnTeamScoreChanged;
        ScoreManager.Instance.IndividualScores.OnListChanged += OnIndividualScoresChanged;

        // Initial draw
        OnTeamScoreChanged(0, ScoreManager.Instance.TeamScore.Value);
        RebuildScoreboard();
    }

    private void OnTeamScoreChanged(int oldValue, int newValue)
    {
        if (teamScoreText != null)
            teamScoreText.text = $"Team Score: {newValue}";
    }

    private void OnIndividualScoresChanged(NetworkListEvent<ScoreManager.PlayerScoreData> evt)
    {
        RebuildScoreboard();
    }

    private void RebuildScoreboard()
    {
        // 1) Clear out existing entries
        foreach (Transform child in contentPanel)
            Destroy(child.gameObject);

        // 2) Copy the NetworkList into a plain List so we can sort it
        var temp = new List<ScoreManager.PlayerScoreData>();
        foreach (var d in ScoreManager.Instance.IndividualScores)
            temp.Add(d);

        // Sort by score, descending
        temp.Sort((a, b) => b.score.CompareTo(a.score));

        // 3) Instantiate one ScoreEntry per player
        foreach (var data in temp)
        {
            var go = Instantiate(scoreEntryPrefab, contentPanel);
            var entry = go.GetComponent<ScoreEntry>();
            if (entry == null)
            {
                Debug.LogError("[ScoreboardUI] scoreEntryPrefab is missing ScoreEntry!");
                continue;
            }

            // FixedString64Bytes  string
            string netName = data.playerName.ToString();
            string displayName = string.IsNullOrEmpty(netName)
                ? PlayerPrefs.GetString("LocalPlayerName", $"Player{data.playerId}")
                : netName;

            entry.Setup(displayName, data.score);
        }
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.TeamScore.OnValueChanged -= OnTeamScoreChanged;
            ScoreManager.Instance.IndividualScores.OnListChanged -= OnIndividualScoresChanged;
        }
    }
}
