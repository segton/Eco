/*using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class DistortionAwareGraphicRaycaster : GraphicRaycaster
{
    [Tooltip("The VolumeProfile that contains your PaniniProjection & LensDistortion")]
    public VolumeProfile volumeProfile;

    PaniniProjection _panini;
    LensDistortion _lens;

    protected override void Awake()
    {
        base.Awake();
        if (volumeProfile != null)
        {
            volumeProfile.TryGet(out _panini);
            volumeProfile.TryGet(out _lens);
        }
    }

    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        // 1) original click in screen-pixels
        Vector2 original = eventData.position;

        // 2) undo post-FX warp
        Vector2 undistorted = InverseDistort(original);

        // 3) build a fresh PointerEventData tied to current EventSystem
        var clone = new PointerEventData(EventSystem.current)
        {
            pointerId = eventData.pointerId,
            position = undistorted,
            delta = eventData.delta,
            pressPosition = eventData.pressPosition,
            clickCount = eventData.clickCount,
            scrollDelta = eventData.scrollDelta,
            button = eventData.button
        };

        // 4) do the usual GraphicRaycaster pass on our corrected coords
        base.Raycast(clone, resultAppendList);

        // 5) restore each result's screenPosition so hovers/tooltips draw under your real cursor
        for (int i = 0; i < resultAppendList.Count; i++)
        {
            var r = resultAppendList[i];
            r.screenPosition = original;
            resultAppendList[i] = r;
        }
    }

    Vector2 InverseDistort(Vector2 screenPos)
    {
        // normalize [0..1]
        var uv = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

        if (_lens != null && _lens.active)
            uv = InverseLensDistortion(uv, _lens);

        if (_panini != null && _panini.active)
            uv = InversePanini(uv, _panini);

        // back to pixel coords
        return new Vector2(uv.x * Screen.width, uv.y * Screen.height);
    }

    Vector2 InverseLensDistortion(Vector2 uv, LensDistortion lens)
    {
        var center = lens.center.value;
        var dir = uv - center;
        float rd = dir.magnitude;
        float k = lens.intensity.value * 0.01f;
        // invert r' = r*(1 + k*r^2) by one Newton iteration
        float r = rd;
        for (int i = 0; i < 2; i++)
            r = rd / (1 + k * r * r);
        dir = (rd > 0 ? dir.normalized * r : Vector2.zero);
        return center + dir;
    }

    Vector2 InversePanini(Vector2 uv, PaniniProjection panini)
    {
        // URP Panini params: distance & screenMatch
        float d = panini.distance.value;
        float s = panini.screenMatch.value;
        // map to [-1..1]
        float xP = (uv.x - 0.5f) * 2f;
        float x = xP;
        // Newton solve for x: f(x)=t/denom - xP = 0
        for (int i = 0; i < 3; i++)
        {
            float t = Mathf.Tan(x * s * 0.5f * Mathf.PI);
            float denom = Mathf.Tan(s * 0.5f * Mathf.PI) * d + (1 - d) * Mathf.Abs(t);
            float f = t / denom - xP;

            // derivative df/dx
            float dt = s * 0.5f * Mathf.PI * (1 + t * t);
            float dden = (1 - d) * (t >= 0 ? dt : -dt);
            float fpx = (dt * denom - t * dden) / (denom * denom);

            x -= f / fpx;
        }
        return new Vector2(x * 0.5f + 0.5f, uv.y);
    }
}
*/