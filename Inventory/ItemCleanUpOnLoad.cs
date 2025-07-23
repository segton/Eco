using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class ItemCleanupOnLoad : MonoBehaviour
{
    private bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsHost) return;

        // only touch Items that actually live in this newly-loaded scene
        foreach (var go in GameObject.FindGameObjectsWithTag("Item"))
        {
            if (go.scene != scene)
                continue;   // skip anything in DontDestroyOnLoad (or other scenes)

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(destroy: true);
            else
                Destroy(go);
        }
    }
}
