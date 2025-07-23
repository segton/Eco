using System;
using System.Collections.Generic;
using Unity.Services.Vivox;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParticipantUIManager : MonoBehaviour
{
    [Header("Prefab & Container")]
    public GameObject participantIndicatorPrefab;
    public Transform participantsContainer;

    class Entry
    {
        public GameObject root;
        public TMP_Text label;
        public Image voiceIcon;
        public Button muteButton;
        public Slider volumeSlider;
        // we no longer store a single VivoxParticipant, but a list of them
        public List<VivoxParticipant> vivoxParticipants = new();
        // one shared delegate to recompute icon scale
        public Action onEnergy;
        public Action onMute;
        public string participantId;
        public string displayName;
    }

    private readonly Dictionary<string, Entry> _rows = new();
    private readonly Dictionary<string, VivoxParticipant> _waiting = new();

    void Awake()
    {
        ScoreManager.Instance.IndividualScores.OnListChanged += OnScoreChanged;
        VivoxService.Instance.ParticipantAddedToChannel += OnVivoxJoined;
        VivoxService.Instance.ParticipantRemovedFromChannel += OnVivoxLeft;
    }

    void OnDestroy()
    {
        ScoreManager.Instance.IndividualScores.OnListChanged -= OnScoreChanged;
        VivoxService.Instance.ParticipantAddedToChannel -= OnVivoxJoined;
        VivoxService.Instance.ParticipantRemovedFromChannel -= OnVivoxLeft;
    }

    void Start()
    {
        foreach (var s in ScoreManager.Instance.IndividualScores)
        {
            string name = s.playerName.ToString();
            string participantId = s.playerId.ToString();
            Debug.Log($"[ParticipantUIManager] Initial score entry - Name: {name}, ID: {participantId}");
            CreateRowAndMaybeWire(name, participantId);
        }
    }

    void OnScoreChanged(NetworkListEvent<ScoreManager.PlayerScoreData> ev)
    {
        string id = ev.Value.playerId.ToString();
        string nick = ev.Value.playerName.ToString();

        // 1) Always handle removals, even if nick == ""
        if (ev.Type == NetworkListEvent<ScoreManager.PlayerScoreData>.EventType.Remove)
        {
            if (_rows.TryGetValue(id, out var entry))
            {
                // tear down energy callbacks
                foreach (var vp in entry.vivoxParticipants)
                    vp.ParticipantAudioEnergyChanged -= entry.onEnergy;
                Unwire(entry);
                Destroy(entry.root);
                _rows.Remove(id);
            }
            return;
        }

        // 2) For adds/updates, never bail on empty—use a placeholder
        if (string.IsNullOrEmpty(nick))
            nick = $"Player{id}";

        if (ev.Type == NetworkListEvent<ScoreManager.PlayerScoreData>.EventType.Add ||
            ev.Type == NetworkListEvent<ScoreManager.PlayerScoreData>.EventType.Value)
        {
            CreateRowAndMaybeWire(nick, id);
            if (_rows.TryGetValue(id, out var entry))
                entry.label.text = nick;
        }
    }

    private void CreateRowAndMaybeWire(string nickname, string participantId)
    {
        Debug.Log($"[ParticipantUIManager] Creating/Wiring row - Name: {nickname}, ID: {participantId}");

        if (_rows.ContainsKey(participantId))
        {
            Debug.Log($"[ParticipantUIManager] Row already exists for {nickname}");
            return;
        }

        var go = Instantiate(participantIndicatorPrefab, participantsContainer);
        go.name = $"Participant_{nickname}";
        var txt = go.GetComponentInChildren<TMP_Text>();
        var iconGO = go.transform.Find("VoiceIcon")?.gameObject;
        var icon = iconGO?.GetComponent<Image>();
        var btn = iconGO?.GetComponent<Button>();
        var slider = go.transform.Find("VolumeSlider")?.GetComponent<Slider>();
        if (txt) txt.text = nickname;

        var entry = new Entry
        {
            root = go,
            label = txt,
            voiceIcon = icon,
            muteButton = btn,
            volumeSlider = slider,
            participantId = participantId,
            displayName = nickname
        };

        // build our energyaggregator callback *once*
        entry.onEnergy = () =>
        {
            if (entry.voiceIcon == null) return;
            // find the maximum AudioEnergy over *all* channels
            float maxEnergy = 0f;
            foreach (var vp in entry.vivoxParticipants)
                maxEnergy = Math.Max(maxEnergy, (float)vp.AudioEnergy);

            float s = Mathf.Clamp(maxEnergy, 0.3f, 1f) + 0.3f;
            entry.voiceIcon.transform.localScale = Vector3.one * s;
            Debug.Log($"[ParticipantUIManager] Aggregated AudioEnergy for {entry.displayName}: {maxEnergy}");
        };

        _rows[participantId] = entry;

        // If Vivox already joined for this ID, wire now
        if (_waiting.TryGetValue(participantId, out var pending))
        {
            Debug.Log($"[ParticipantUIManager] Found queued Vivox join for {participantId}");
            OnVivoxJoined(pending);
            _waiting.Remove(participantId);
        }
    }

    private void OnVivoxJoined(VivoxParticipant p)
    {
        Debug.Log($"[ParticipantUIManager] Vivox participant joined - Name: {p.DisplayName}, PlayerId: {p.PlayerId}");
        var id = p.DisplayName;
        if (_rows.TryGetValue(id, out var entry))
        {
            // if this is the *first* channel, do your full Wire (mute, slider, etc.)
            if (entry.vivoxParticipants.Count == 0)
                Wire(entry, p);

            // in *all* cases, hook up energy
            entry.vivoxParticipants.Add(p);
            p.ParticipantAudioEnergyChanged += entry.onEnergy;
            // and immediately pulse once
            entry.onEnergy();
        }
        else
        {
            Debug.Log($"[ParticipantUIManager] No matching row found, buffering participant {p.DisplayName}");
            _waiting[id] = p;
        }
    }

    private void OnVivoxLeft(VivoxParticipant p)
    {
        Debug.Log($"[ParticipantUIManager] Vivox participant left - Name: {p.DisplayName}, PlayerId: {p.PlayerId}");
        var id = p.DisplayName;
        _waiting.Remove(id);

        if (_rows.TryGetValue(id, out var entry))
        {
            // Remove this channel’s energy callback
            if (entry.vivoxParticipants.Remove(p))
                p.ParticipantAudioEnergyChanged -= entry.onEnergy;

            // If no more channels left, tear down the entire row
            if (entry.vivoxParticipants.Count == 0)
            {
                Debug.Log($"[ParticipantUIManager] Removing UI row for {entry.displayName}");
                // 1) Unhook any remaining delegates
                Unwire(entry);

                // 2) Destroy the participant’s UI GameObject
                Destroy(entry.root);

                // 3) Remove from our dictionary
                _rows.Remove(id);

                // 4) Force a layout rebuild so spacing updates immediately
                var rt = participantsContainer.GetComponent<RectTransform>();
                if (rt != null)
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            else
            {
                // Otherwise re-pulse (in case they were speaking only in the closed channel)
                entry.onEnergy();
            }
        }
    }

    private void Wire(Entry e, VivoxParticipant p)
    {
        Debug.Log($"[ParticipantUIManager] Wiring participant - Entry Name: {e.displayName}, Vivox Name: {p.DisplayName}");
        // initial icon state for mute
        if (e.voiceIcon != null)
            e.voiceIcon.color = p.IsMuted
                ? new Color(1, 1, 1, 0.3f)
                : Color.white;

        // subscribe to mute toggles
        e.onMute = () =>
        {
            if (e.voiceIcon != null)
                e.voiceIcon.color = p.IsMuted
                    ? new Color(1, 1, 1, 0.3f)
                    : Color.white;
        };
        p.ParticipantMuteStateChanged += e.onMute;

        // toggle local mute on button click
        if (e.muteButton != null)
        {
            e.muteButton.onClick.RemoveAllListeners();
            e.muteButton.onClick.AddListener(() =>
            {
                Debug.Log($"[ParticipantUIManager] Mute button clicked for {p.DisplayName}");
                if (p.IsMuted) p.UnmutePlayerLocally();
                else p.MutePlayerLocally();
                e.onMute?.Invoke();
            });
        }

        // volume slider
        if (e.volumeSlider != null)
        {
            e.volumeSlider.minValue = -50f;
            e.volumeSlider.maxValue = 50f;
            e.volumeSlider.value = p.LocalVolume;
            e.volumeSlider.onValueChanged.RemoveAllListeners();
            e.volumeSlider.onValueChanged.AddListener(v =>
            {
                Debug.Log($"[ParticipantUIManager] Volume changed for {p.DisplayName}: {v}");
                p.SetLocalVolume(Mathf.RoundToInt(v));
            });
        }
    }

    private void Unwire(Entry e)
    {
        // remove mute listener
        if (e.vivoxParticipants.Count > 0)
        {
            var anyVivox = e.vivoxParticipants[0];
            anyVivox.ParticipantMuteStateChanged -= e.onMute;
        }
        // (audioEnergy already unsubscribed on leave)
    }
}
