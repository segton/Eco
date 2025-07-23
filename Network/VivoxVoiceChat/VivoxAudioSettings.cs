using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VivoxAudioSettings : MonoBehaviour
{
    [Header("Toggle Settings")]
    public KeyCode toggleKey = KeyCode.F3;
    public GameObject uiContents;

    [Header("Device Selection")]
    public TMP_Dropdown inputDeviceDropdown;
    public TMP_Dropdown outputDeviceDropdown;

    [Header("Volume Controls")]
    [Tooltip("Mic (input) volume: –50 to +50")]
    public Slider inputVolumeSlider;
    [Tooltip("Master output volume: –50 to +50")]
    public Slider masterOutputVolumeSlider;
    //[Tooltip("Per-channel output slider: –50 to +50")]
    //public Slider channelVolumeSlider;

    //[Tooltip("Which channel the per-channel slider controls")]
    //public string channelName;

    [Header("Effective Device Display")]
    public TMP_Text effectiveInputDeviceText;
    public TMP_Text effectiveOutputDeviceText;

    [Header("Local Mic Energy Meter")]
    public Image deviceEnergyMask;
    const float kVoiceMeterSpeed = 3f;

    void Awake()
    {
        VivoxService.Instance.AvailableInputDevicesChanged += RefreshInputDevices;
        VivoxService.Instance.AvailableOutputDevicesChanged += RefreshOutputDevices;
        VivoxService.Instance.EffectiveInputDeviceChanged += UpdateEffectiveInputText;
        VivoxService.Instance.EffectiveOutputDeviceChanged += UpdateEffectiveOutputText;
    }

    void OnDestroy()
    {
        VivoxService.Instance.AvailableInputDevicesChanged -= RefreshInputDevices;
        VivoxService.Instance.AvailableOutputDevicesChanged -= RefreshOutputDevices;
        VivoxService.Instance.EffectiveInputDeviceChanged -= UpdateEffectiveInputText;
        VivoxService.Instance.EffectiveOutputDeviceChanged -= UpdateEffectiveOutputText;
    }

    void OnEnable()
    {
        RefreshInputDevices();
        RefreshOutputDevices();
        UpdateEffectiveInputText();
        UpdateEffectiveOutputText();

        // configure slider ranges
        inputVolumeSlider.minValue = -50f; inputVolumeSlider.maxValue = 50f;
        masterOutputVolumeSlider.minValue = -50f; masterOutputVolumeSlider.maxValue = 50f;
        //channelVolumeSlider.minValue = -50f; channelVolumeSlider.maxValue = 50f;

        // hook UI
        inputDeviceDropdown.onValueChanged.AddListener(OnInputDeviceSelected);
        outputDeviceDropdown.onValueChanged.AddListener(OnOutputDeviceSelected);
        inputVolumeSlider.onValueChanged.AddListener(OnInputVolumeChanged);
        masterOutputVolumeSlider.onValueChanged.AddListener(OnMasterOutputVolumeChanged);
        //channelVolumeSlider.onValueChanged.AddListener(OnChannelVolumeChanged);

        // sync initial values
        inputVolumeSlider.value = VivoxService.Instance.InputDeviceVolume;
        masterOutputVolumeSlider.value = VivoxService.Instance.OutputDeviceVolume;
        //channelVolumeSlider.value = 0f; // we don't pull this back
    }

    void OnDisable()
    {
        inputDeviceDropdown.onValueChanged.RemoveListener(OnInputDeviceSelected);
        outputDeviceDropdown.onValueChanged.RemoveListener(OnOutputDeviceSelected);
        inputVolumeSlider.onValueChanged.RemoveListener(OnInputVolumeChanged);
        masterOutputVolumeSlider.onValueChanged.RemoveListener(OnMasterOutputVolumeChanged);
        //channelVolumeSlider.onValueChanged.RemoveListener(OnChannelVolumeChanged);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleUI();

        // localmic VU meter
        var self = VivoxService.Instance.ActiveChannels
                    .SelectMany(kv => kv.Value)
                    .FirstOrDefault(p => p.IsSelf);
        if (self != null)
        {
            float e = (float)self.AudioEnergy;
            deviceEnergyMask.fillAmount = Mathf.Lerp(
                deviceEnergyMask.fillAmount,
                e,
                Time.deltaTime * kVoiceMeterSpeed
            );
        }
    }

    void ToggleUI()
    {
        bool on = !uiContents.activeSelf;
        uiContents.SetActive(on);
        Cursor.visible = on;
        Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked;
    }

    //  Device Dropdown Callbacks 

    void RefreshInputDevices()
    {
        inputDeviceDropdown.ClearOptions();
        var names = VivoxService.Instance.AvailableInputDevices
                       .Select(d => d.DeviceName).ToList();
        inputDeviceDropdown.AddOptions(names);
        int idx = VivoxService.Instance.AvailableInputDevices
                    .IndexOf(VivoxService.Instance.ActiveInputDevice);
        if (idx >= 0) inputDeviceDropdown.SetValueWithoutNotify(idx);
    }

    void RefreshOutputDevices()
    {
        outputDeviceDropdown.ClearOptions();
        var names = VivoxService.Instance.AvailableOutputDevices
                       .Select(d => d.DeviceName).ToList();
        outputDeviceDropdown.AddOptions(names);
        int idx = VivoxService.Instance.AvailableOutputDevices
                    .IndexOf(VivoxService.Instance.ActiveOutputDevice);
        if (idx >= 0) outputDeviceDropdown.SetValueWithoutNotify(idx);
    }

    async void OnInputDeviceSelected(int i)
        => await VivoxService.Instance.SetActiveInputDeviceAsync(
               VivoxService.Instance.AvailableInputDevices[i]
           );

    async void OnOutputDeviceSelected(int i)
        => await VivoxService.Instance.SetActiveOutputDeviceAsync(
               VivoxService.Instance.AvailableOutputDevices[i]
           );

    void UpdateEffectiveInputText()
        => effectiveInputDeviceText.text =
               $"Input: {VivoxService.Instance.EffectiveInputDevice.DeviceName}";

    void UpdateEffectiveOutputText()
        => effectiveOutputDeviceText.text =
               $"Output: {VivoxService.Instance.EffectiveOutputDevice.DeviceName}";

    //  Volume Slider Callbacks 

    void OnInputVolumeChanged(float v)
        => VivoxService.Instance.SetInputDeviceVolume(Mathf.RoundToInt(v));

    void OnMasterOutputVolumeChanged(float v)
    {
        int vol = Mathf.RoundToInt(v);
        VivoxService.Instance.SetOutputDeviceVolume(vol);
        foreach (var chan in VivoxService.Instance.ActiveChannels.Keys)
            _ = VivoxService.Instance.SetChannelVolumeAsync(chan, vol);
    }

    /*void OnChannelVolumeChanged(float v)
        => _ = VivoxService.Instance.SetChannelVolumeAsync(
               channelName, Mathf.RoundToInt(v)
           );*/
}
