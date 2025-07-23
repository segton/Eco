using System;
using System.Collections.Generic;
using System.Linq;                //  make sure this is here
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine.UI;      //  for VivoxService & VivoxParticipant

public class SpectateUIManager : MonoBehaviour
{
    [Header("UI Prefab & Container")]
    public GameObject entryPrefab;    // must have a TMP_Text child and a child named "VoiceIcon"
    public Transform container;       // with your VerticalLayoutGroup
    [Header("Voice Channel to Track (exact name)")]
    public string channelToTrackPrefix = "DeadChannel";
    // track the instantiated UI rows
    Dictionary<string, GameObject> _entries = new();
    // track each player’s energy handler so we can unsubscribe
    private readonly Dictionary<string, Action> _energyHandlers = new();
    private readonly Dictionary<string, (VivoxParticipant p, Action speechH, Action energyH)> _handlers
    = new Dictionary<string, (VivoxParticipant, Action, Action)>();

    // ---- 2) subscribe in Awake / unsubscribe in OnDestroy ----
    void Awake()
    {
        VivoxService.Instance.ParticipantAddedToChannel += OnVivoxParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnVivoxParticipantRemoved;
    }

    void OnDestroy()
    {
        VivoxService.Instance.ParticipantAddedToChannel -= OnVivoxParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel -= OnVivoxParticipantRemoved;
    }
    void OnEnable()
    {
        // 1) Vivox joins/leaves
        VivoxService.Instance.ParticipantAddedToChannel += OnVivoxParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnVivoxParticipantRemoved;

        // 2) Netcode clients connect/disconnect
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // 3) health-driven: register every existing player
        foreach (var pm in FindObjectsOfType<PlayerMovement>())
            RegisterPlayerHealth(pm);
    }

    void OnDisable()
    {
        VivoxService.Instance.ParticipantAddedToChannel -= OnVivoxParticipantAdded;
        VivoxService.Instance.ParticipantRemovedFromChannel -= OnVivoxParticipantRemoved;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        // clean up all rows & handlers
        foreach (var key in _entries.Keys.ToList())
            RemoveEntry(key);
    }
    private void OnClientConnected(ulong clientId)
    {
        var pm = FindObjectsOfType<PlayerMovement>()
                 .FirstOrDefault(p => p.OwnerClientId == clientId);
        if (pm != null) RegisterPlayerHealth(pm);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // if they disconnect, clean up instantly
        RemoveEntry(clientId.ToString());
    }

    private void RegisterPlayerHealth(PlayerMovement pm)
    {
        string key = pm.OwnerClientId.ToString();
        // fire Add/Remove whenever health crosses zero
        pm.Health.OnValueChanged += (_, newH) =>
        {
            if (newH <= 0) AddEntry(key);
            else RemoveEntry(key);
        };
        // if they’re already dead when this UI appears
        if (pm.Health.Value <= 0)
            AddEntry(key);
    }
    // ---- 3) when Vivox tells you a participant has joined ANY channel ----
    private void OnVivoxParticipantAdded(VivoxParticipant p)
    {
        if (p.IsSelf) return;
        string key = p.DisplayName;
        // only hook energy for rows that already exist
        if (!_entries.ContainsKey(key) || _energyHandlers.ContainsKey(key))
            return;

        var go = _entries[key];
        var icon = go.transform.Find("VoiceIcon")?.GetComponent<Image>();
        if (icon == null) return;

        icon.gameObject.SetActive(true);

        Action energyH = () =>
        {
            float e = (float)p.AudioEnergy;
            float s = Mathf.Clamp(e, 0.1f, 1f) + 0.3f;
            icon.transform.localScale = Vector3.one * s;
        };
        p.ParticipantAudioEnergyChanged += energyH;
        _energyHandlers[key] = energyH;

        // init immediately
        energyH();
    }

    // ---- 4) when Vivox tells you they left that channel ----
    private void OnVivoxParticipantRemoved(VivoxParticipant p)
    {
        string key = p.PlayerId;
        if (_energyHandlers.TryGetValue(key, out var energyH))
        {
            p.ParticipantAudioEnergyChanged -= energyH;
            _energyHandlers.Remove(key);
        }
    }
    private void AddEntry(string key)
    {
        if (_entries.ContainsKey(key)) return;

        // 1) instantiate row
        var go = Instantiate(entryPrefab, container);
        go.name = "Spect_" + key;

        // 2) set display name
        var txt = go.GetComponentInChildren<TMP_Text>();
        string displayName = null;
        if (txt != null && ulong.TryParse(key, out var cid))
        {
            displayName = GetDisplayName(cid);
            txt.text = displayName;
        }

        // 3) find the VivoxParticipant in the correct channel
        var vp = VivoxService.Instance.ActiveChannels
               .Where(kv => kv.Key.StartsWith(channelToTrackPrefix, StringComparison.OrdinalIgnoreCase))
               .SelectMany(kv => kv.Value)
               .FirstOrDefault(p => p.DisplayName == key);

        if (vp == null)
        {
            Debug.LogWarning($"[SpectateUI] no participant “{displayName}” in any channel starting with “{channelToTrackPrefix}”");
            _entries[key] = go;
            return;
        }

        // 4) hook the VoiceIcon
        var iconGO = go.transform.Find("VoiceIcon")?.gameObject;
        if (iconGO != null)
        {
            var icon = iconGO.GetComponent<Image>();
            iconGO.SetActive(false);

            // toggle on speech start/stop
            Action speechH = () =>
            {
                iconGO.SetActive(vp.SpeechDetected);
            };
            vp.ParticipantSpeechDetected += speechH;

            // pulse while speaking
            Action energyH = () =>
            {
                if (!iconGO.activeSelf) return;
                float e = (float)vp.AudioEnergy;
                float s = Mathf.Clamp(e, 0.1f, 1f) + 0.3f;
                icon.transform.localScale = Vector3.one * s;
            };
            vp.ParticipantAudioEnergyChanged += energyH;

            // seed initial state
            speechH();
            energyH();

            // remember for cleanup
            _handlers[key] = (vp, speechH, energyH);
        }

        _entries[key] = go;
    }
    // ---- 5) also clean up if your healthdriven RemoveEntry fires ----
    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
    /*void Update()
    {
        foreach (var kv in _entries)
        {
            string key = kv.Key;        // PlayerId string
            GameObject go = kv.Value;
            var iconGO = go.transform.Find("VoiceIcon")?.gameObject;
            if (iconGO == null) continue;

            // 1) Gather every AudioEnergy for this player in every channel:
            var energies = VivoxService.Instance.ActiveChannels
                              .Values
                              .SelectMany(ch => ch)
                              .Where(p => p.PlayerId == key)
                              .Select(p => (float)p.AudioEnergy)
                              .ToList();

            if (energies.Count == 0)
            {
                Debug.LogWarning($"[SpectateUI] {key} not found in any Vivox channel");
                continue;
            }

            // 2) Log them so you can see which channels have non-zero:
            Debug.Log($"[SpectateUI] {key} energies = [{string.Join(", ", energies)}]");

            // 3) Pick the loudest:
            float e = energies.Max();

            // 4) Scale the icon as before:
            float scale = Mathf.Clamp(e, 0.1f, 1f) + 0.3f;
            iconGO.transform.localScale = Vector3.one * scale;
        }
    }
    */
    private void RemoveEntry(string key)
    {
        // 1) UI tear-down
        if (_entries.TryGetValue(key, out var go))
        {
            Destroy(go);
            _entries.Remove(key);
        }

        // 2) Vivox unhook
        if (_handlers.TryGetValue(key, out var tuple))
        {
            var (vp, speechH, energyH) = tuple;
            vp.ParticipantSpeechDetected -= speechH;
            vp.ParticipantAudioEnergyChanged -= energyH;
            _handlers.Remove(key);
        }
    }
    private string GetDisplayName(ulong clientId)
    {
        // try ScoreManager
        if (ScoreManager.Instance != null)
        {
            foreach (var e in ScoreManager.Instance.IndividualScores)
                if (e.playerId == clientId)
                    return e.playerName.ToString();
        }
        // fallback to Vivox
        string key = clientId.ToString();
        var vp = VivoxService.Instance.ActiveChannels
                    .Values.SelectMany(ch => ch)
                    .FirstOrDefault(p => p.PlayerId == key);
        if (vp != null) return vp.DisplayName;
        // last resort
        return clientId.ToString();
    }
}