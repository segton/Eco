using UnityEngine;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ComputerInteraction : NetworkBehaviour
{
    [Header("Cameras (priority swap)")]
    [SerializeField] private CinemachineCamera vcamPlayer;    // your FPS vcam
    [SerializeField] private CinemachineCamera vcamTerminal;  // your terminal vcam
    [SerializeField] private Camera playerCamera;    // the “real” Camera

    [Header("Disable While UI Active")]
    [SerializeField] private MonoBehaviour movementScript;
    [SerializeField] private MonoBehaviour lookScript;
    [SerializeField] private CinemachineInputAxisController cinemachineInputController;
    [Header("Interaction Settings")]
    [SerializeField] private LayerMask terminalLayer;
    [SerializeField] private float interactionRadius = 2f;

    [Header("Terminalcam Facing Offset")]
    [SerializeField] private Vector3 anchorRotationOffset = Vector3.zero;

    // internal state
    private ComputerTerminal currentTerminal;
    private Transform anchorTransform;
    private GameObject uiCanvasInstance;
    private UICursorLimiter cursorLimiter;
    private bool isInteracting;
    private int playerDefaultPrio;
    private int terminalDefaultPrio;

    void OnEnable()
    {
        // this fires on _any_ local scene change
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        // this fires when Netcode finishes loading a scene for a client
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnNetworkSceneLoadComplete;
    }
    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnNetworkSceneLoadComplete;
    }

    // called whenever Unity’s SceneManager switches the active scene
    void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (isInteracting)
            EndInteraction();
    }

    // called whenever Netcode finishes loading a networked scene
    void OnNetworkSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // only care about our own client
        if (clientId == NetworkManager.Singleton.LocalClientId && isInteracting)
            EndInteraction();
    }

    void Awake()
    {
        playerDefaultPrio = vcamPlayer.Priority;
        terminalDefaultPrio = vcamTerminal.Priority;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (!Keyboard.current.fKey.wasPressedThisFrame) return;

        if (isInteracting) EndInteraction();
        else TryEnter();
    }

    private void TryEnter()
    {
        var hits = Physics.OverlapSphere(
            playerCamera.transform.position,
            interactionRadius,
            terminalLayer
        );
        foreach (var hit in hits)
        {
            var term = hit.GetComponentInParent<ComputerTerminal>();
            if (term == null) continue;

            currentTerminal = term;
            anchorTransform = term.CameraAnchor;
            uiCanvasInstance = term.UICanvas;
            cursorLimiter = uiCanvasInstance.GetComponent<UICursorLimiter>();
            BeginInteraction();
            return;
        }
    }

    private void BeginInteraction()
    {
        isInteracting = true;
        movementScript.enabled = false;
        lookScript.enabled = false;
        cinemachineInputController.enabled = false;
        // swap priorities
        vcamPlayer.Priority = playerDefaultPrio;
        vcamTerminal.Priority = playerDefaultPrio + 1;

        // follow position only, disable Aim so rotation stays manual
        vcamTerminal.Follow = anchorTransform;
        var aim = vcamTerminal.GetCinemachineComponent(CinemachineCore.Stage.Aim) as MonoBehaviour;
        if (aim != null) aim.enabled = false;

        // snap exactly onto anchor + yaw offset
        var worldY = anchorTransform.eulerAngles.y + anchorRotationOffset.y;
        var camT = vcamTerminal.transform;
        camT.position = anchorTransform.position;
        camT.rotation = Quaternion.Euler(0, worldY, 0);

        // go live
        vcamTerminal.Prioritize();

        // show  wire UI canvas
        uiCanvasInstance.SetActive(true);
        if (uiCanvasInstance.TryGetComponent<Canvas>(out var c))
            c.worldCamera = playerCamera;

        // activate your cursorclamp
        cursorLimiter?.Activate();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EndInteraction()
    {
        isInteracting = false;
        movementScript.enabled = true;
        lookScript.enabled = true;
        uiCanvasInstance.SetActive(false);
        cinemachineInputController.enabled = true;
        // restore Aim pipeline
        var aim = vcamTerminal.GetCinemachineComponent(CinemachineCore.Stage.Aim) as MonoBehaviour;
        if (aim != null) aim.enabled = true;

        // restore camera priorities
        vcamPlayer.Priority = playerDefaultPrio;
        vcamTerminal.Priority = terminalDefaultPrio;
        vcamPlayer.Prioritize();

        // turn off cursorclamp
        cursorLimiter?.Deactivate();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerCamera.transform.position, interactionRadius);
        }
    }
}
