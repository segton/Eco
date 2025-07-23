using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;
using Unity.Cinemachine;

public class VivoxListenerPositioner : MonoBehaviour
{
    [Tooltip("Set at runtime by VoiceChannelManager")]
    public string ProximityChannelName;

    CinemachineBrain _brain;
    bool _channelReady;

    async void Start()
    {
        // Wait for VivoxService
        while (VivoxService.Instance == null)
            await Task.Yield();

        VivoxService.Instance.LoggedIn += HandleVivoxLoggedIn;
        VivoxService.Instance.ChannelJoined += HandleChannelJoined;

        if (VivoxService.Instance.IsLoggedIn)
            HandleVivoxLoggedIn();

        _brain = FindObjectOfType<CinemachineBrain>();
    }

    void HandleVivoxLoggedIn()
    {
        // once you log in, grab it from your channel manager
        ProximityChannelName = VoiceChannelManager.proximityChannelName;
        TryEnableChannel();
    }

    void HandleChannelJoined(string channelName)
    {
        // sometimes our channel manager string isn't set yet, 
        // so let's double-check here too
        if (channelName.StartsWith("ProximityVoice"))
            ProximityChannelName = channelName;

        TryEnableChannel();
    }

    void TryEnableChannel()
    {
        var vivox = VivoxService.Instance;
        if (vivox.IsLoggedIn
         && !string.IsNullOrEmpty(ProximityChannelName)
         && vivox.ActiveChannels.ContainsKey(ProximityChannelName))
        {
            Debug.Log($"[VivoxListener] Found your proximity channel '{ProximityChannelName}'");
            _channelReady = true;
        }
    }

    void LateUpdate()
    {
        if (!_channelReady)
        {
            Debug.Log("[VivoxListener] skipping because channel not ready");
            return;
        }
        if (_brain == null)
        {
            // first try Camera.main
            if (Camera.main != null)
                _brain = Camera.main.GetComponent<CinemachineBrain>();

            // fallback: search entire scene
            if (_brain == null)
                _brain = FindObjectOfType<CinemachineBrain>();

            if (_brain == null)
            {
                Debug.LogWarning("[VivoxListener] still no CinemachineBrain, waiting...");
                return;
            }
        }
        var cam = _brain.OutputCamera;
        if (cam == null)
        {
            Debug.Log("[VivoxListener] skipping because Brain.OutputCamera is still null");
            return;
        }

        Debug.Log("[VivoxListener] Pushing spectator-cam pos to Vivox");
        var t = cam.transform;
        VivoxService.Instance.Set3DPosition(
            speakerPos: t.position,
            listenerPos: t.position,
            listenerAtOrient: t.forward,
            listenerUpOrient: t.up,
            channelName: ProximityChannelName
        );
    }

    void OnDestroy()
    {
        if (VivoxService.Instance != null)
        {
            VivoxService.Instance.LoggedIn -= HandleVivoxLoggedIn;
            VivoxService.Instance.ChannelJoined -= HandleChannelJoined;
        }
    }
}

