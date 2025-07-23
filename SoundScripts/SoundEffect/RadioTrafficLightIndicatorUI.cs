using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class RadioTrafficLightIndicatorUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The parent GameObject (e.g. an Image) to show/hide.")]
    [SerializeField] private GameObject indicatorObject;
    [Tooltip("The Image component whose color we’ll tint.")]
    [SerializeField] private Image indicatorImage;

    [Header("Traffic Light Colors")]
    [SerializeField] private Color colorOtherSpeaking = Color.red;
    [SerializeField] private Color colorListening = Color.yellow;
    [SerializeField] private Color colorLocalSpeaking = Color.green;

    InventoryManager _inv;
    RadioChannelManager _radio;

    void Awake()
    {
        // find your InventoryManager (should be on your local player)
        _inv = FindObjectOfType<InventoryManager>();
        _radio = RadioChannelManager.Instance;
        if (_radio == null)
            Debug.LogError("RadioTrafficLightIndicatorUI: no RadioChannelManager in scene!");
    }

    void Start()
    {
        // subscribe to speaker changes
        if (_radio != null)
            _radio.CurrentRadioSpeaker.OnValueChanged += OnSpeakerChanged;

        // hide until you actually have a walkie
        indicatorObject.SetActive(false);

        // initial draw
        RefreshUI(_radio?.CurrentRadioSpeaker.Value ?? RadioChannelManager.NoSpeaker);
    }

    void OnDestroy()
    {
        if (_radio != null)
            _radio.CurrentRadioSpeaker.OnValueChanged -= OnSpeakerChanged;
    }

    void Update()
    {
        // show/hide based on whether you currently *carry* a charged walkie
        bool hasWalkie = _inv != null && _inv.CheckIfInventoryContainsWalkie();
        if (indicatorObject.activeSelf != hasWalkie)
            indicatorObject.SetActive(hasWalkie);
    }

    private void OnSpeakerChanged(ulong oldSpeaker, ulong newSpeaker)
    {
        RefreshUI(newSpeaker);
    }

    private void RefreshUI(ulong speaker)
    {
        // if you don’t even have a walkie, stay hidden
        if (_inv == null || !_inv.CheckIfInventoryContainsWalkie())
        {
            indicatorObject.SetActive(false);
            return;
        }

        indicatorObject.SetActive(true);

        ulong local = NetworkManager.Singleton.LocalClientId;
        ulong nobody = RadioChannelManager.NoSpeaker;

        if (speaker == local)
        {
            indicatorImage.color = colorLocalSpeaking;
            Debug.Log("[RTLI] GREEN (you speaking)");
        }
        else if (speaker == nobody)
        {
            indicatorImage.color = colorListening;
            Debug.Log("[RTLI] YELLOW (listening)");
        }
        else
        {
            indicatorImage.color = colorOtherSpeaking;
            Debug.Log("[RTLI] RED (other speaking)");
        }
    }
}
