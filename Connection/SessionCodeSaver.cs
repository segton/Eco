using TMPro;
using UnityEngine;

public class SessionCodeSaver : MonoBehaviour
{
    // Assume the join code text is in a TextMeshPro component on this GameObject (or one of its children).
    private TMP_Text joinCodeText;

    private void Awake()
    {
        // Try to get the TMP_Text component (adjust if needed to search in children)
        joinCodeText = GetComponentInChildren<TMP_Text>();
        if (joinCodeText == null)
        {
            Debug.LogError("[JoinCodePreserver] No TMP_Text component found on this GameObject or its children!");
        }
    }

    private void Update()
    {
        if (joinCodeText != null)
        {
            // Update the static join code every frame (or you can do this less frequently)
            SessionData.LastJoinCode = joinCodeText.text;
        }
    }
}
