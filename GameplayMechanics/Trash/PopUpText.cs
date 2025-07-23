using UnityEngine;
using TMPro;

public class PopUpText : MonoBehaviour
{
    [Tooltip("Assign the TextMesh Pro component from the prefab here.")]
    public TMP_Text textDisplay;

    /// <summary>
    /// Sets the text of the pop-up.
    /// </summary>
    public void SetText(string text)
    {
        if (textDisplay != null)
        {
            textDisplay.text = text;
        }
        else
        {
            Debug.LogWarning("[PopUpText] textDisplay is not assigned!");
        }
    }
}
