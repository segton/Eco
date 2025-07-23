using TMPro;
using UnityEngine;

public class DisplayJoinCode : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;

    private void Start()
    {
        if (codeText == null)
        {
            Debug.LogError("[DisplayJoinCode] No TMP_Text reference assigned!");
            return;
        }

        // Set the text to the join code saved in SessionData
        codeText.text = SessionData.LastJoinCode;
    }
}
