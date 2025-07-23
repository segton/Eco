using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Vivox;

public class VoiceIndicator : MonoBehaviour
{
    public Text voiceStatusText;   // Assign a UI Text in the Inspector
    public Image micIcon;          // Assign a UI Image (e.g., a microphone icon)

    // This method should be called from a channel session's participant event.
    public void UpdateVoiceIndicator(VivoxParticipant participant)
    {
        if (participant.IsSelf)
        {
            bool isSpeaking = participant.SpeechDetected;
            if (voiceStatusText != null)
            {
                voiceStatusText.text = isSpeaking ? "Speaking" : "Silent";
            }
            if (micIcon != null)
            {
                micIcon.color = isSpeaking ? Color.green : Color.white;
            }
        }
    }
}
