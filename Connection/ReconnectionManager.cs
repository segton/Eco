using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class ReconnectionManager : MonoBehaviour
{
    public static ReconnectionManager Instance { get; private set; }
    private bool isReconnecting = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to disconnection and connection events.
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    // Called when a client disconnects.
    private void HandleClientDisconnect(ulong clientId)
    {
        // If the local client disconnects, attempt to reconnect.
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogWarning($"[ReconnectionManager] Local client {clientId} disconnected.");
            // Optionally show a UI prompt here.
            StartCoroutine(AttemptReconnect());
        }
        else
        {
            Debug.Log($"[ReconnectionManager] Remote client {clientId} disconnected.");
        }
    }

    // Called when a client connects (or reconnects).
    private void HandleClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log($"[ReconnectionManager] Local client {clientId} connected.");
            // At this point, you may restore persistent data or notify your UI.
        }
    }

    // A coroutine that attempts to reconnect the local client.
    private IEnumerator AttemptReconnect()
    {
        if (isReconnecting)
            yield break;
        isReconnecting = true;

        // Optionally, wait for a moment before retrying.
        yield return new WaitForSeconds(3f);

        Debug.Log("[ReconnectionManager] Attempting to reconnect...");

        // If still disconnected, try to start the client.
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkManager.Singleton.StartClient();
        }

        // Optionally, wait for the connection to establish.
        float timeout = 10f;
        float timer = 0f;
        while (!NetworkManager.Singleton.IsConnectedClient && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("[ReconnectionManager] Reconnected successfully!");
            // Optionally, load the current scene or restore state if necessary.
            // For example: SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            Debug.LogError("[ReconnectionManager] Reconnect timed out.");
            // Optionally prompt the user for a manual reconnect.
        }

        isReconnecting = false;
    }
}
