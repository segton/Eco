using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class DungeonSettingsUIController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The button that kicks off map generation")]
    [SerializeField] private Button generateButton;
    [Tooltip("A little label that reads “Only for conductor”")]
    [SerializeField] private GameObject onlyForConductorText;

    void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("DungeonSettingsUIController: no NetworkManager in scene!");
            return;
        }

        // Host (conductor) = true if this client started as Host
        bool isConductor = NetworkManager.Singleton.IsHost;

        // Enable the generate button only for the host...
        if (generateButton != null)
            generateButton.interactable = isConductor;

        // ...and show the Only for conductor label to everyone else
        if (onlyForConductorText != null)
            onlyForConductorText.SetActive(!isConductor);
    }
}
