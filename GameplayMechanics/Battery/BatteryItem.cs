using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System;
using Unity.Services.Vivox;
using Unity.UI.Shaders.Sample;

public class BatteryItem : MonoBehaviour
{
    [Header("UI Indicators")]
    [Tooltip("Show this image when the battery is in use")]
    [SerializeField] private Image inUseImage;
    [Tooltip("Static identifier for the item type.")]
    public string itemID;

    [Tooltip("Unique identifier for this instance (should persist across drops and hotbar switches).")]
    public string uniqueItemID;

    [Tooltip("Current battery level at runtime.")]
    public float currentBatteryLevel;

    [Tooltip("Maximum (initial) battery level.")]
    public float initialBatteryLevel = 100f;

    [Tooltip("Battery drain per second if 'in use'.")]
    public float drainRate = 1f;

    // Flags for battery drain.
    public bool isInUse = false;
    public bool isHeld = false;
    public bool isLocalHolder = false;

    [Tooltip("Custom Battery bar (Image+SciFiMeter)")]
    public CustomSlider customSlider;

    private float updateInterval = 0.5f;
    private float updateTimer = 0f;

    [Tooltip("Optional main script to disable at 0 battery (e.g., WalkieTalkie).")]
    public MonoBehaviour mainFunctionalityScript;
    private bool functionalityDisabled = false;

    private float emptyThreshold = 2f; // Threshold below which battery is considered empty.

    private void Awake()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (string.IsNullOrEmpty(uniqueItemID))
            {
                uniqueItemID = Guid.NewGuid().ToString();
                if (BatteryManager.Instance != null)
                    currentBatteryLevel = BatteryManager.Instance.GetBatteryLevel(uniqueItemID, initialBatteryLevel);
                else
                    currentBatteryLevel = initialBatteryLevel;
            }
        }
        UpdateInUseUI();
        ConfigureSlider();
    }
    private void UpdateInUseUI()
    {
        if (inUseImage == null) return;
        inUseImage.gameObject.SetActive(isInUse && isHeld && isLocalHolder);
    }


    public void ConfigureSlider()
    {
        if (customSlider != null)
           {
                       // normalize [0..1] before setting
            float t = currentBatteryLevel / initialBatteryLevel;
            customSlider.SetNormalizedValue(t);
            customSlider.gameObject.SetActive(isLocalHolder);
           }
        else
        {
            Debug.LogWarning("[BatteryItem] batteryBar reference is null!", this);
        }
    }

    private void Update()
    {
        if (isHeld && isInUse && isLocalHolder)
        {
            float oldLevel = currentBatteryLevel;
            currentBatteryLevel = Mathf.Max(currentBatteryLevel - drainRate * Time.deltaTime, 0f);

            if (!Mathf.Approximately(oldLevel, currentBatteryLevel))
            {
                if (customSlider != null)
                    customSlider.SetNormalizedValue(currentBatteryLevel / initialBatteryLevel);
                // If on server, update BatteryManager.
                if (NetworkManager.Singleton.IsServer && BatteryManager.Instance != null)
                {
                    BatteryManager.Instance.SaveBatteryLevel(uniqueItemID, currentBatteryLevel);
                }

                updateTimer += Time.deltaTime;
                if (updateTimer >= updateInterval)
                {
                    updateTimer = 0f;
                    ForceUpdateBatteryOnServer();
                }

                // If battery just dropped below the empty threshold, force update to 0.
                if (currentBatteryLevel <= emptyThreshold && oldLevel > emptyThreshold)
                {
                    Debug.Log("[BatteryItem] Battery dropped below threshold; forcing update to 0.");
                    ForceUpdateBatteryOnServer(0f);
                }

                Debug.Log($"[BatteryItem] ({uniqueItemID}) Battery draining: {currentBatteryLevel}");
            }

            if (currentBatteryLevel <= emptyThreshold && !functionalityDisabled)
            {
                functionalityDisabled = true;
                DisableMainFunctionality();
            }
            else if (currentBatteryLevel > emptyThreshold && functionalityDisabled)
            {
                functionalityDisabled = false;
                EnableMainFunctionality();
            }
        }
    }

    /// <summary>
    /// Forces an update of the battery level on the server by using the local player's InventoryManager.
    /// </summary>
    private void ForceUpdateBatteryOnServer(float? overrideValue = null)
    {
        float finalValue = overrideValue.HasValue ? overrideValue.Value : currentBatteryLevel;
        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayer != null)
        {
            var inv = localPlayer.GetComponent<InventoryManager>();
            if (inv != null)
            {
                inv.UpdateBatteryLevelServerRpc(inv.selectedSlot.Value, finalValue);
            }
        }
    }

    private void DisableMainFunctionality()
    {
        if (mainFunctionalityScript != null)
        {
            mainFunctionalityScript.enabled = false;
            Debug.Log("[BatteryItem] Main functionality disabled due to battery empty.");
            WalkieTalkie wt = mainFunctionalityScript as WalkieTalkie;
            if (wt != null && wt.IsTransmitting)
            {
                wt.StopTalk();
            }
            // Optionally mute the walkie channel.
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn &&
                VivoxService.Instance.ActiveChannels.ContainsKey(VoiceChannelManager.walkieChannelName))
            {
                _ = VivoxService.Instance.SetChannelVolumeAsync(VoiceChannelManager.walkieChannelName, -80);
            }
        }
    }

    private void EnableMainFunctionality()
    {
        if (mainFunctionalityScript != null)
        {
            mainFunctionalityScript.enabled = true;
            Debug.Log("[BatteryItem] Main functionality re-enabled.");
            WalkieTalkie wt = mainFunctionalityScript as WalkieTalkie;
            if (wt != null && VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn &&
                VivoxService.Instance.ActiveChannels.ContainsKey(VoiceChannelManager.walkieChannelName))
            {
                _ = VivoxService.Instance.SetChannelVolumeAsync(VoiceChannelManager.walkieChannelName, 0);
            }
        }
    }

    public void inUse(bool yes)
    {
        if (!isHeld || !isLocalHolder)
        {
            Debug.LogWarning($"[BatteryItem] inUse({yes}) ignored on {uniqueItemID}: isHeld={isHeld}, isLocalHolder={isLocalHolder}");
            return;
        }

        isInUse = yes;
        UpdateInUseUI();
    }

    public void SetHeld(bool held)
    {
        isHeld = held;
        ConfigureSlider();
    }

    public void Recharge(float amount)
    {
        currentBatteryLevel = Mathf.Min(currentBatteryLevel + amount, initialBatteryLevel);
        Debug.Log($"[BatteryItem] Recharged by {amount}, new level = {currentBatteryLevel}");
        ConfigureSlider();
        if (currentBatteryLevel > emptyThreshold && functionalityDisabled)
        {
            functionalityDisabled = false;
            EnableMainFunctionality();
        }
        ForceUpdateBatteryOnServer();
    }

    public void RechargeFull()
    {
        currentBatteryLevel = initialBatteryLevel;
        Debug.Log("[BatteryItem] Fully recharged.");
        ConfigureSlider();
        if (functionalityDisabled)
        {
            functionalityDisabled = false;
            EnableMainFunctionality();
        }
        ForceUpdateBatteryOnServer();
    }
}
