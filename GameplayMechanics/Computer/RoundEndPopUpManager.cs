using System.Collections;
using System.Linq;
using UnityEngine;
using TMPro;

public class RoundEndPopupManager : MonoBehaviour
{
    public static RoundEndPopupManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text debtCountText;
    [SerializeField] private TMP_Text quotaText;
    [SerializeField] private TMP_Text playerDeadCountText;  // 

    [Header("Popup Timing")]
    [SerializeField] public float displayDuration = 3f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);  // optional, if you want persistence
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        panel.SetActive(false);
    }

    /// <summary>
    /// Call this on every client via a ClientRpc.
    /// </summary>
    public void ShowPopup(int score, bool success, int debtCount, int newQuota)
    {
        // Score and pass/fail
        scoreText.text = $"Final Score: {score}";
        resultText.text = success ? "Quota Met!" : "Quota Not Met";
        debtCountText.text = success ? "" : $"Debts: {debtCount}";

        // Next quota
        quotaText.text = $"Next Quota: {newQuota}";

        // Count how many players are marked dead
        int numDead = 0;
        if (PlayerStateManager.Instance != null)
        {
            numDead = PlayerStateManager
                .Instance
                .GetAllDeadStatuses()
                .Count(kv => kv.Value);
        }
        playerDeadCountText.text = $"Players Dead: {numDead}";

        // Show & schedule hide
        panel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        panel.SetActive(false);
    }
}
