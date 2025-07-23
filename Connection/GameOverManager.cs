using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class GameOverManager : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string gameplaySceneName = "DefaultScene";
    [SerializeField] private string mainMenuSceneName = "StartScene";

    [Header("UI References")]
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text roundsCompletedText;
    [SerializeField] private TMP_Text debtCountText;

    [Header("Player Control Scripts to Disable")]
    [Tooltip("Enter the class names (no namespace) of the MonoBehaviours you want disabled on Game Over.")]
    [SerializeField] private string[] controlScriptTypeNames;

    private Type[] controlTypes;

    private void Awake()
    {
        // Resolve the Type objects from the names list
        var allTypes = AppDomain.CurrentDomain
                                .GetAssemblies()
                                .SelectMany(a => a.GetTypes())
                                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                                .ToArray();

        var resolved = new List<Type>();
        foreach (var name in controlScriptTypeNames)
        {
            var t = allTypes.FirstOrDefault(x => x.Name == name);
            if (t != null)
                resolved.Add(t);
            else
                Debug.LogWarning($"[GameOverManager] Could not find a MonoBehaviour named '{name}'.");
        }
        controlTypes = resolved.ToArray();
    }

    private void Start()
    {
        // Populate stats
        if (ScoreManager.Instance != null)
            finalScoreText.text = $"Final Score: {ScoreManager.Instance.TeamScore.Value}";

        var brain = FindObjectOfType<GameBrain>();
        if (brain != null)
        {
            roundsCompletedText.text = $"Rounds: {brain.RoundsCompleted.Value}";
            debtCountText.text = $"Debts: {brain.DebtCount.Value}";
        }

        // Unlock mouse
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Disable only the listed control scripts
        DisableAllPlayers();
    }

    private void DisableAllPlayers()
    {
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
        {
            foreach (var t in controlTypes)
            {
                var comp = player.GetComponent(t) as Behaviour;
                if (comp != null)
                    comp.enabled = false;
            }
        }
    }

    private void EnableAllPlayers()
    {
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
        {
            foreach (var t in controlTypes)
            {
                var comp = player.GetComponent(t) as Behaviour;
                if (comp != null)
                    comp.enabled = true;
            }
        }
    }

    public void OnRetryPressed()
    {
        // Re-enable any scripts we disabled before leaving
        EnableAllPlayers();

        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                gameplaySceneName,
                LoadSceneMode.Single
            );
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            RequestRetryServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRetryServerRpc(ServerRpcParams rpcParams = default)
    {
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameplaySceneName,
            LoadSceneMode.Single
        );
    }

    public void OnMainMenuPressed()
    {
        // Re-enable any scripts we disabled before leaving
        EnableAllPlayers();
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[GameOverManager] Shutting down NetworkManager before Main Menu.");
            NetworkManager.Singleton.Shutdown();
        }
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                mainMenuSceneName,
                LoadSceneMode.Single
            );
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            RequestMainMenuServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMainMenuServerRpc(ServerRpcParams rpcParams = default)
    {
        NetworkManager.Singleton.SceneManager.LoadScene(
            mainMenuSceneName,
            LoadSceneMode.Single
        );
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}
