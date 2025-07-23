using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SettingsPanelController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Button closeButton;

    private MouseLook localMouseLook;

    void Start()
    {
        settingsPanel.SetActive(false);

        // find the one MouseLook that belongs to this client
        foreach (var ml in FindObjectsOfType<MouseLook>())
        {
            if (ml.IsOwner)
            {
                localMouseLook = ml;
                break;
            }
        }
        if (localMouseLook == null)
            Debug.LogError("No local MouseLook found!");

        if (localMouseLook != null)
            sensitivitySlider.value = localMouseLook.mouseSensitivity;

        sensitivitySlider.onValueChanged.AddListener(val => {
            if (localMouseLook != null)
                localMouseLook.mouseSensitivity = val;
        });
        closeButton.onClick.AddListener(TogglePanel);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            TogglePanel();
    }

    private void TogglePanel()
    {
        bool open = !settingsPanel.activeSelf;
        settingsPanel.SetActive(open);

        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;

        if (localMouseLook != null)
            localMouseLook.enabled = !open;
    }
}
