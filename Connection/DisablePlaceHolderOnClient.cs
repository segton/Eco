using UnityEngine;
using Unity.Netcode;

public class DisablePlaceholderOnClient : MonoBehaviour
{
    void Awake()
    {
        // If this instance is not running as host, disable this object immediately.
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
        {
            gameObject.SetActive(false);
        }
    }
}
