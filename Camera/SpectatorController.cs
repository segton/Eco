using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using TMPro;
using Unity.Services.Vivox;
public class SpectatorController : NetworkBehaviour
{
    [Header("Cinemachine Cameras")]
    [SerializeField] CinemachineCamera vcamPlay;   // your FPS cam (Priority = 3)
    [SerializeField] CinemachineCamera vcamSpec;   // your spectator cam (Priority = 0)


    [Header("Disable On Death")]
    [SerializeField] PlayerMovement movement;      // your movement script
    [SerializeField] MonoBehaviour lookScript;     // your MouseLook script


    [Header("Spectate UI")]
    [SerializeField] private TMP_Text spectatedNameText; // drag in your “Spectated: ___” label
    List<PlayerMovement> livePlayers = new List<PlayerMovement>();
    int currentIndex;

    Animator _animator;
    AudioListener _audioListener;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // store components once
        _animator = GetComponentInChildren<Animator>();
        _audioListener = GetComponentInChildren<AudioListener>();

        // initial camera priorities
        vcamPlay.Priority = 3;
        vcamSpec.Priority = 0;
    }

    void Update()
    {
        if (!IsOwner) return;

        // when we just died...
        if (PlayerStateManager.Instance.IsDead && vcamSpec.Priority == 0)
            EnterSpectatorMode();

        // Only allow cycling while dead
        if (PlayerStateManager.Instance.IsDead)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) Cycle(-1);
            if (Keyboard.current.dKey.wasPressedThisFrame) Cycle(+1);
        }
    }

    public void ExitSpectatorMode()
    {
        // 1) re-enable player controls
        movement.enabled = true;
        lookScript.enabled = true;

        // 2) re-enable animator
        if (_animator) _animator.enabled = true;

        // 3) put your audio listener back on you
        if (_audioListener) _audioListener.enabled = true;

        // 4) swap camera priorities back
        vcamPlay.Priority = 3;
        vcamSpec.Priority = 0;


    }

    void EnterSpectatorMode()
    {
        // 1) disable movement & look
        movement.enabled = false;
        lookScript.enabled = false;

        // 2) disable animator
        if (_animator) _animator.enabled = false;

        // 3) switch off your audio listener so it doesn’t stay on your corpse
        if (_audioListener) _audioListener.enabled = false;

        // 4) swap camera priorities
        vcamPlay.Priority = 0;
        vcamSpec.Priority = 5;

        // 5) build list & snap to first live player
        RefreshLiveList();
        if (livePlayers.Count > 0)
            SwitchTo(livePlayers[0].transform);
    }

    void RefreshLiveList()
    {
        livePlayers = FindObjectsOfType<PlayerMovement>()
            .Where(pm => pm.Health.Value > 0 && pm != movement)
            .ToList();
        currentIndex = 0;
    }

 
    void Cycle(int dir)
    {
        if (livePlayers.Count == 0) return;
        currentIndex = (currentIndex + dir + livePlayers.Count) % livePlayers.Count;
        var target = livePlayers[currentIndex].transform;
        SwitchTo(target);
        UpdateSpectatedName(target);
        Debug.Log($"[Spectator] now spectating index={currentIndex}, clientId={livePlayers[currentIndex].OwnerClientId}");
    }
    void UpdateSpectatedName(Transform target)
    {
        var pm = target.GetComponent<PlayerMovement>();
        if (pm == null || spectatedNameText == null || ScoreManager.Instance == null)
            return;

        // 1) Try ScoreManager with a simple foreach
        string displayName = "";
        foreach (var entry in ScoreManager.Instance.IndividualScores)
        {
            if (entry.playerId == pm.OwnerClientId)
            {
                displayName = entry.playerName.ToString();
                break;
            }
        }

        // 2) Fallback to Vivox if we didn’t find it
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = VivoxService.Instance.ActiveChannels
                .SelectMany(kv => kv.Value)
                .FirstOrDefault(p => p.PlayerId == pm.OwnerClientId.ToString())
                ?.DisplayName;
        }

        // 3) Final fallback to PlayerPrefs/raw ID
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = PlayerPrefs
                .GetString("LocalPlayerName", $"Player{pm.OwnerClientId}");
        }

        spectatedNameText.text = $"Spectating: {displayName}";
        Debug.Log($"[Spectator] Updated spectatedNameText to '{displayName}'");
    }
    void SwitchTo(Transform target)
    {
        vcamSpec.Follow = target;
        vcamSpec.LookAt = target;
        vcamSpec.Prioritize();    // make it the live cam immediately
                                  // ---- new: update the spectated name ----
        var pm = target.GetComponent<PlayerMovement>();
        if (pm != null && spectatedNameText != null && ScoreManager.Instance != null)
        {
            string displayName = null;
            foreach (var entry in ScoreManager.Instance.IndividualScores)
            {
                if (entry.playerId == pm.OwnerClientId)
                {
                    displayName = entry.playerName.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(displayName))
            {
                var vp = Unity.Services.Vivox.VivoxService.Instance.ActiveChannels
                             .SelectMany(kv => kv.Value)
                             .FirstOrDefault(p => p.PlayerId == pm.OwnerClientId.ToString());
                if (vp != null)
                    displayName = vp.DisplayName;
                else
                    displayName = PlayerPrefs.GetString("LocalPlayerName", $"Player{pm.OwnerClientId}");
            }

            spectatedNameText.text = $"Spectating: {displayName}";
        }
    }
}
