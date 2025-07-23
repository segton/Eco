using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
using System.Text;

public class ScoreDisplay : MonoBehaviour
{
    [Header("UI Text Components")]
    [Tooltip("Shows the total team score.")]
    [SerializeField] private TMP_Text teamScoreText;

    [Tooltip("Multi-line text for individual player scores.")]
    [SerializeField] private TMP_Text individualScoresText;

    [Tooltip("Shows the current quota required this round.")]
    [SerializeField] private TMP_Text quotaText;

    private GameBrain gameBrain;
    private void Start()
    {
        if (ScoreManager.Instance == null)
        {
            Debug.LogError("[ScoreDisplay] ScoreManager instance not found.");
            return;
        }

        gameBrain = FindObjectOfType<GameBrain>();
                if (gameBrain == null)
                    {
            Debug.LogError("[ScoreDisplay] GameBrain not found.");
                    }
                else
                    {
                        // Subscribe to quota changes
            gameBrain.CurrentQuota.OnValueChanged += OnQuotaChanged;
                        // Initial display
            UpdateQuotaText(gameBrain.CurrentQuota.Value);
                    }

        // Subscribe to updates
        ScoreManager.Instance.TeamScore.OnValueChanged += OnTeamScoreChanged;
        ScoreManager.Instance.IndividualScores.OnListChanged += OnIndividualScoresChanged;

        // Initial population
        UpdateTeamScoreText(ScoreManager.Instance.TeamScore.Value);
        UpdateIndividualScoresText();
    }
    private void OnQuotaChanged(int oldQuota, int newQuota)
    {
        UpdateQuotaText(newQuota);
    }

    private void UpdateQuotaText(int quota)
    {
            if (quotaText != null)
        quotaText.text = $"Quota: {quota}";
        }
private void OnTeamScoreChanged(int _, int current)
    {
        UpdateTeamScoreText(current);
    }

    private void OnIndividualScoresChanged(NetworkListEvent<ScoreManager.PlayerScoreData> _)
    {
        UpdateIndividualScoresText();
    }

    private void UpdateTeamScoreText(int score)
    {
        if (teamScoreText != null)
            teamScoreText.text = $"Team Score: {score}";
    }
    void Awake()
    {
        // Legacy networkvar callback
        ScoreManager.Instance.TeamScore.OnValueChanged += OnTeamScoreChanged;
        // New static event (fires even on host)
        ScoreManager.OnTeamScoreUpdated += UpdateTeamScoreText;
    }

    
    private void UpdateIndividualScoresText()
    {
        if (individualScoresText == null || ScoreManager.Instance == null)
            return;

        // 1) Copy out the NetworkList into a plain List
        var tempList = new List<ScoreManager.PlayerScoreData>();
        foreach (var data in ScoreManager.Instance.IndividualScores)
        {
            tempList.Add(data);
        }
        
        // 2) Sort by descending score
        tempList.Sort((a, b) => b.score.CompareTo(a.score));

        // 3) Build the multi-line string
        var sb = new StringBuilder();
        foreach (var data in tempList)
        {
            // Convert FixedString64Bytes to string
            string netName = data.playerName.ToString();
            // Fallback if empty
            string displayName = string.IsNullOrEmpty(netName)
                ? PlayerPrefs.GetString("LocalPlayerName", $"Player{data.playerId}")
                : netName;

            sb.AppendLine($"{displayName}: {data.score}");
        }

        individualScoresText.text = sb.ToString().TrimEnd();
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.TeamScore.OnValueChanged -= OnTeamScoreChanged;
            ScoreManager.Instance.IndividualScores.OnListChanged -= OnIndividualScoresChanged;
            ScoreManager.OnTeamScoreUpdated -= UpdateTeamScoreText;

        }
        if (gameBrain != null)
            gameBrain.CurrentQuota.OnValueChanged -= OnQuotaChanged;
    }
  
}
