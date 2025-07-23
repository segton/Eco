using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class MouseLook : NetworkBehaviour
{
    public static MouseLook LocalInstance { get; private set; }


    public float mouseSensitivity = 400f;
    public Camera playerCamera;
    public AudioListener playerAudioListener;

    private float xRotation = 0f;
    private float yRotation = 0f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            return;
        }
        LocalInstance = this;

        if (playerCamera != null) playerCamera.enabled = true;
        if (playerAudioListener != null) playerAudioListener.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsOwner) return;
        HandleMouseLook();
        // (no more UpdateNetworkRotation or body rotates here)
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null) return;

        float mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity * Time.deltaTime;
        float mouseY = Mouse.current.delta.y.ReadValue() * mouseSensitivity * Time.deltaTime;

        // 1) Accumulate yaw on the camera
        yRotation += mouseX;

        // 2) Accumulate & clamp pitch
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // 3) Apply both to camera
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
    }
}
