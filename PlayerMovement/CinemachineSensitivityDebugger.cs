using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Netcode;  // for NetworkObject

public class CinemachineSensitivityDebugger : MonoBehaviour
{
    [Header("Drag your Unity UI Slider here")]
    [SerializeField] private Slider sensitivitySlider;

    private CinemachineInputAxisController axisController;
    private float currentSens;

    void Awake()
    {
        Debug.Log("[CMC] Awake on " + gameObject.name);
    }

    void Start()
    {
        Debug.Log("[CMC] Start – finding local InputAxisController…");

        // Find all controllers, pick the one under our local player
        var all = FindObjectsOfType<CinemachineInputAxisController>();
        foreach (var c in all)
        {
            var netObj = c.GetComponentInParent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                axisController = c;
                break;
            }
        }
        // fallback to first if no owned camera found
        if (axisController == null && all.Length > 0)
            axisController = all[0];

        if (axisController == null)
        {
            Debug.LogError("[CMC] X No CinemachineInputAxisController found in scene!");
            enabled = false;
            return;
        }

        Debug.Log($"[CMC] > Using controller on '{axisController.gameObject.name}'");

        if (sensitivitySlider == null)
        {
            Debug.LogError("[CMC] X sensitivitySlider not assigned in Inspector!");
            enabled = false;
            return;
        }

        Debug.Log($"[CMC] Slider assigned (min={sensitivitySlider.minValue}, max={sensitivitySlider.maxValue})");

        // Wire slider changes
        sensitivitySlider.onValueChanged.AddListener(OnSliderChanged);

        // Initialize
        OnSliderChanged(sensitivitySlider.value);
    }

    private void OnSliderChanged(float newValue)
    {
        currentSens = newValue;
        Debug.Log($"[CMC] -> OnSliderChanged: newSens={newValue}");

        // Legacy path override (for raw->scaled logs)
        CinemachineCore.GetInputAxis = axisName =>
        {
            float raw = Input.GetAxis(axisName);
            float scaled = raw * currentSens;
            Debug.Log($"[CMC]   LegacyAxis '{axisName}': raw={raw:F3} -> scaled={scaled:F3}");
            return scaled;
        };

        // New-Input-System path override
        axisController.ReadControlValueOverride = (action, hints, context, defaultReader) =>
        {
            float raw = defaultReader(action, hints, context, null);
            float scaled = raw * currentSens;
            Debug.Log($"[CMC]   NewInput '{action.name}': raw={raw:F3} -> scaled={scaled:F3}");
            return scaled;
        };

        // Finally, push into the component’s Inspectable Gain/LegacyGain fields
        CinemachineGainTweaker.SetGain(axisController, currentSens);
    }
}
