using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;

public static class CinemachineGainTweaker
{
    /// <summary>
    /// Walks the public Controllers list, grabs each Reader instance,
    /// and sets Reader.Gain = gain * originalSign, Reader.LegacyGain = gain.
    /// </summary>
    public static void SetGain(CinemachineInputAxisController ctrl, float gain)
    {
        if (ctrl == null)
        {
            Debug.LogError("[GainTweaker] Controller is null!");
            return;
        }

        // 1) Grab the Controllers property (IEnumerable of Controller structs)
        var prop = ctrl.GetType()
            .GetProperty("Controllers", BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        {
            Debug.LogError("[GainTweaker] No Controllers property found!");
            return;
        }

        var controllers = prop.GetValue(ctrl) as System.Collections.IEnumerable;
        if (controllers == null)
        {
            Debug.LogError("[GainTweaker] Controllers not enumerable!");
            return;
        }

        int applied = 0;
        foreach (var c in controllers)
        {
            var ct = c.GetType();

            // 2) Find the Reader field inside each Controller
            var readerField = ct.GetFields(
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Instance
                )
                .FirstOrDefault(f => f.FieldType == typeof(CinemachineInputAxisController.Reader));
            if (readerField == null)
                continue;

            var reader = readerField.GetValue(c) as CinemachineInputAxisController.Reader;
            if (reader == null)
                continue;

            // 3) Preserve original sign of the Gain (for Y-invert)
            float orig = reader.Gain;
            float sign = Mathf.Sign(orig);
            if (sign == 0) sign = 1; // safety

            reader.Gain = gain * sign;
            reader.LegacyGain = gain;

            applied++;
        }

        Debug.Log($"[GainTweaker] Applied gain={gain} to {applied} Reader(s) on '{ctrl.gameObject.name}'");
    }
}
