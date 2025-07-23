using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject), typeof(InventoryManager))]
public class RadioTrafficLightIndicator : MonoBehaviour
{
    [Header("UI Elements (child of this prefab)")]
    [Tooltip("Drag in the GameObject (e.g. an Image) you want shown/hidden.")]
    [SerializeField] private GameObject indicatorObject;
    [Tooltip("Drag in the Image component whose color we'll tint.")]
    [SerializeField] private Image indicatorImage;

    [Header("Traffic-Light Colors")]
    [SerializeField] private Color colorOtherSpeaking = Color.red;
    [SerializeField] private Color colorListening = Color.yellow;
    [SerializeField] private Color colorLocalSpeaking = Color.green;

    private InventoryManager _inv;
    private RadioChannelManager _radio;
    private NetworkVariable<ulong>.OnValueChangedDelegate _onSpeakerChanged;

    void Awake()
    {
        // Hide immediately so you never see the default or stale state
        if (indicatorObject != null)
            indicatorObject.SetActive(false);
    }

    void Start()
    {
        var netObj = GetComponent<NetworkObject>();
        // Only run this on your localplayer instance
        if (netObj.OwnerClientId != NetworkManager.Singleton.LocalClientId)
        {
            enabled = false;
            return;
        }

        _inv = GetComponent<InventoryManager>();
        _radio = RadioChannelManager.Instance;
        if (_radio == null)
        {
            Debug.LogError("RadioTrafficLightIndicator: no RadioChannelManager in scene");
            enabled = false;
            return;
        }

        // Cache and subscribe the delegate so we can unsubscribe later
        _onSpeakerChanged = (oldSpeaker, newSpeaker) => RefreshUI(newSpeaker);
        _radio.CurrentRadioSpeaker.OnValueChanged += _onSpeakerChanged;

        // Initial draw
        RefreshUI(_radio.CurrentRadioSpeaker.Value);
    }

    void Update()
    {
        // Show or hide the indicator based on whether you carry a charged walkie
        bool hasWalkie = _inv.CheckIfInventoryContainsWalkie();
        if (indicatorObject.activeSelf != hasWalkie)
            indicatorObject.SetActive(hasWalkie);

        // If you have it, keep the color in sync
        if (hasWalkie)
            RefreshUI(_radio.CurrentRadioSpeaker.Value);
    }

    void OnDestroy()
    {
        if (_radio != null && _onSpeakerChanged != null)
            _radio.CurrentRadioSpeaker.OnValueChanged -= _onSpeakerChanged;
    }

    private void RefreshUI(ulong speaker)
    {
        // If no walkie or no image, bail
        if (!_inv.CheckIfInventoryContainsWalkie() || indicatorImage == null)
            return;

        // Ensure it's visible once you're carrying the walkie
        indicatorObject.SetActive(true);

        ulong local = NetworkManager.Singleton.LocalClientId;
        ulong nobody = RadioChannelManager.NoSpeaker;

        if (speaker == local)
        {
            // You are speaking
            indicatorImage.color = colorLocalSpeaking;
        }
        else if (speaker == nobody)
        {
            // Nobody speaking
            indicatorImage.color = colorListening;
        }
        else
        {
            // Someone else is speaking
            indicatorImage.color = colorOtherSpeaking;
        }
    }
}
