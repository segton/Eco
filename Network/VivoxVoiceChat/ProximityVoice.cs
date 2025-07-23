using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
using Unity.Netcode;

public class ProximityVoice : NetworkBehaviour
{
    // We'll retrieve the channel name at runtime.
    private string proximityChannelName;

    private async void Start()
    {
        if (!IsOwner)
            return;

        // Wait until Vivox is initialized and logged in.
        while (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn)
        {
            await Task.Delay(100);
        }

        // Retrieve the channel name from VoiceChannelManager.
        // (Make sure VoiceChannelManager.InitializeChannels(sessionId) has been called earlier.)
        proximityChannelName = VoiceChannelManager.proximityChannelName;
        if (string.IsNullOrEmpty(proximityChannelName))
        {
            Debug.LogError("[ProximityVoice] Proximity channel name is null or empty.");
            return;
        }

        StartCoroutine(Update3DPosition());
    }

    private IEnumerator Update3DPosition()
    {
        while (true)
        {
            // Only try to update if the channel name is valid.
            if (!string.IsNullOrEmpty(proximityChannelName) &&
                VivoxService.Instance.ActiveChannels.ContainsKey(proximityChannelName))
            {
                VivoxService.Instance.Set3DPosition(
                    transform.position,
                    transform.position,
                    transform.forward,
                    Vector3.up,
                    proximityChannelName
                );
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}
