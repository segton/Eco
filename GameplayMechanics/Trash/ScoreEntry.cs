using UnityEngine;
using TMPro;

public class ScoreEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text entryText;

    /// <summary>
    /// Call this to set the display name and score.
    /// </summary>
    public void Setup(string displayName, int score)
    {
        entryText.text = $"{displayName}: {score}";
    }
}
