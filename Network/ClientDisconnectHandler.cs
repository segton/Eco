using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ClientDisconnectHandler : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    private bool wasConnected = true;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += _ => BeginCleanExit();
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null) return;
        bool isConnected = NetworkManager.Singleton.IsConnectedClient;
        if (wasConnected && !isConnected && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            BeginCleanExit();
        wasConnected = isConnected;
    }

    private void BeginCleanExit()
    {
        // 1) Fire the widget’s Leave() under-the-hood
        InvokeLeaveSessionWidget();

        // 2) After a short delay, do the real cleanup
        StartCoroutine(ProceedToMainMenuAfterDelay(0.75f));
    }

    private IEnumerator ProceedToMainMenuAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 3) Tear down Netcode
        NetworkManager.Singleton?.Shutdown();

        // 4) Restore cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 5) Load the Main Menu
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != mainMenuSceneName) return;

        // 6) Re-enable all your UI: buttons, input fields, toggles, etc.
        foreach (var sel in Resources.FindObjectsOfTypeAll<Selectable>())
            sel.interactable = true;

        foreach (var tmp in Resources.FindObjectsOfTypeAll<TMP_InputField>())
        {
            tmp.interactable = true;
            tmp.readOnly = false;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void InvokeLeaveSessionWidget()
    {
        // find the internal LeaveSession component by name
        var allButtons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (var btn in allButtons)
        {
            var leaveComp = btn.gameObject.GetComponent("Unity.Multiplayer.Widgets.LeaveSession");
            if (leaveComp != null)
            {
                Debug.Log("[ClientDisconnectHandler] Invoking widgets onClick");
                btn.onClick.Invoke();
                return;
            }
        }
    }
}
