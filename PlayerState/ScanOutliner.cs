using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;  // <-- add this

public class ScanOutliner : MonoBehaviour
{
    [Header("Scan Settings")]
    public string interactableTag = "Item";
    public float maxScanDistance = 10f;
    public LayerMask obstacleMask;

    [Header("Outline Appearance")]
    public Color baseOutlineColor = Color.green;
    [Range(1f, 20f)] public float baseOutlineWidth = 5f;
    public float pulseFrequency = 2f;
    [Range(0f, 1f)] public float pulseMagnitude = 0.3f;

    [Header("Label UI")]
    public GameObject labelPrefab;
    public float labelOffsetY = 0.5f;

    [Header("References")]
    public Camera playerCam;

    readonly List<Outline> _outlined = new();
    readonly Dictionary<GameObject, GameObject> _labels = new();

    void Update()
    {
        if (playerCam == null) return;
        bool scanning = Input.GetMouseButton(1);
        if (!scanning) { ClearAll(); return; }

        ClearAll();
        float pulse = 1f + pulseMagnitude * Mathf.Sin(Time.time * pulseFrequency * 2f * Mathf.PI);
        Vector3 camPos = playerCam.transform.position;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCam);



        var items = GameObject.FindGameObjectsWithTag(interactableTag);
        Debug.Log($"[ScanOutliner] Found {items.Length} objects tagged “{interactableTag}”");

        foreach (var go in items)
        {
            var itemComps = go.GetComponent<Item>();
            if (itemComps != null && itemComps.isHeld &&
                itemComps.lastOwnerId == NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log($"[ScanOutliner] Skipping held item: {go.name} (isHeld={itemComps.isHeld})");
                continue;
            }
            var col = go.GetComponent<Collider>();
            if (col == null) { Debug.LogWarning($"[ScanOutliner] {go.name} missing Collider"); continue; }

            if (!GeometryUtility.TestPlanesAABB(planes, col.bounds)) { Debug.Log($"[ScanOutliner] {go.name} outside frustum"); continue; }

            float dist = Vector3.Distance(camPos, col.bounds.center);
            if (dist > maxScanDistance) { Debug.Log($"[ScanOutliner] {go.name} too far ({dist:F1})"); continue; }

            Vector3 dir = (col.bounds.center - camPos).normalized;
            if (Physics.Raycast(camPos, dir, out var hit, dist, obstacleMask)) { Debug.Log($"[ScanOutliner] {go.name} blocked by {hit.collider.name}"); continue; }

            // Outline every Renderer
            foreach (var rend in go.GetComponentsInChildren<Renderer>())
            {
                var outline = rend.gameObject.GetComponent<Outline>() ?? rend.gameObject.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = baseOutlineColor * pulse;
                float avgScale = (rend.transform.lossyScale.x + rend.transform.lossyScale.y + rend.transform.lossyScale.z) / 3f;
                outline.OutlineWidth = (baseOutlineWidth / avgScale) * pulse;
                _outlined.Add(outline);
                Debug.Log($"  • Outlined {rend.gameObject.name}");
            }

            // Label creation
            if (labelPrefab != null && !_labels.ContainsKey(go))
            {
                var label = Instantiate(labelPrefab);
                // try TextMeshPro first
                var tmp = label.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    var itemComp = go.GetComponent<Item>();
                    var entry = ItemDatabase.Instance?.GetItem(itemComp.itemID);
                    tmp.text = entry != null ? entry.itemName : itemComp?.itemID ?? go.name;
                    Debug.Log($"[ScanOutliner] TMP label for {go.name}: “{tmp.text}”");
                }
                else
                {
                    // fallback to legacy Text
                    var txt = label.GetComponentInChildren<Text>();
                    if (txt != null)
                    {
                        var itemComp = go.GetComponent<Item>();
                        var entry = ItemDatabase.Instance?.GetItem(itemComp.itemID);
                        txt.text = entry != null ? entry.itemName : itemComp?.itemID ?? go.name;
                        Debug.Log($"[ScanOutliner] UI Text label for {go.name}: “{txt.text}”");
                    }
                    else
                    {
                        Debug.LogError($"[ScanOutliner] {labelPrefab.name} has no TMP or Text child!");
                    }
                }

                var bb = label.GetComponent<Billboard>();
                bb?.Initialize(playerCam);
                _labels[go] = label;
            }

            if (_labels.TryGetValue(go, out var lbl))
            {
                lbl.SetActive(true);
                lbl.transform.position = col.bounds.center + Vector3.up * labelOffsetY;
            }
        }
    }

    void ClearAll()
    {
        foreach (var o in _outlined) if (o) o.OutlineWidth = 0;
        _outlined.Clear();
        foreach (var kv in _labels) if (kv.Value) kv.Value.SetActive(false);
    }
}
