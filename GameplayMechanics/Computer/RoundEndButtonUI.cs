using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class RoundEndButtonUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The Panel (child of this Canvas) that holds your End Round button and any decorations.")]
    [SerializeField] private GameObject panel;
    [Tooltip("The Button inside that panel that triggers an early end.")]
    [SerializeField] private Button endRoundButton;

    [Header("Scene Names")]
    [Tooltip("All play/dungeon scenes where the End Round button should appear.")]
    [SerializeField] private List<string> playSceneNames = new List<string>();
    [Tooltip("Exact name of your start/lobby scene, e.g. \"StartScene\"")]
    [SerializeField] private string startSceneName;

    private void Awake()
    {
        // Hide the panel by default
        if (panel != null)
            panel.SetActive(false);

        if (endRoundButton != null)
            endRoundButton.onClick.AddListener(OnEndRoundClicked);
    }

    private void Start()
    {
        // Subscribe to the Netcode scene-load events
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;

        if (endRoundButton != null)
            endRoundButton.onClick.RemoveListener(OnEndRoundClicked);
    }

    private void OnSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // Only show the panel when *this* client has loaded the play scene
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            bool isPlay = playSceneNames.Contains(sceneName);
            bool isStart = sceneName == startSceneName;

            if (panel != null)
                panel.SetActive(isPlay);

            // Optionally hide when back to start
            if (panel != null && isStart)
                panel.SetActive(false);
        }
    }

    private void OnEndRoundClicked()
    {
        // Fire the RPC to end the round on the server
        var mgr = FindObjectOfType<TrainSafetyManager>();
        if (mgr != null)
            mgr.EndRoundEarlyServerRpc();
        else
            Debug.LogWarning("RoundEndButtonUI: no TrainSafetyManager found in scene!");
    }
}
