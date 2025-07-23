using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class BatteryBar : MonoBehaviour
{
    [Tooltip("Name of the float property in your SciFi Meter shader that controls fill.")]
    [SerializeField] string _fillProperty = "_Fill";

    Material _mat;

    void Awake()
    {
        var img = GetComponent<Image>();
        if (img == null)
        {
            Debug.LogError("BatteryBar requires an Image component on the same GameObject!", this);
            enabled = false;
            return;
        }

        // Always instantiate so we don't overwrite the shared material
        _mat = new Material(img.material)
        {
            hideFlags = HideFlags.DontSave
        };
        img.material = _mat;
    }

    /// <summary>
    /// value: currentBatteryLevel, max: initialBatteryLevel
    /// </summary>
    public void SetValue(float value, float max)
    {
        if (_mat == null)
        {
            Debug.LogError("BatteryBar material is null – did Awake() run correctly?", this);
            return;
        }
        float t = Mathf.Clamp01(value / max);
        _mat.SetFloat(_fillProperty, t);
    }
}
