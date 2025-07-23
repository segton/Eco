using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkSceneLoader : MonoBehaviour
{
    [Header("Door Animation")]
    [Tooltip("Animator on your door GameObject")]
    [SerializeField] private Animator doorAnimator;
    [Tooltip("Bool parameter name that controls the closed state")]
    [SerializeField] private string isClosedParam = "IsClosed";
    [Tooltip("Duration (in seconds) of your door closing animation")]
    [SerializeField] private float closeDuration = 1f;

    /// <summary>
    /// Call this on the host to close the door, load everyone into the new scene,
    /// then open the door when the scene is active.
    /// </summary>
    public void LoadLevel(string sceneName)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        StartCoroutine(DoLoadWithDoor(sceneName));
    }

    private IEnumerator DoLoadWithDoor(string sceneName)
    {
        // 1) Set the door closed
        if (doorAnimator != null)
            doorAnimator.SetBool(isClosedParam, true);

        // 2) Wait for the close animation to finish
        yield return new WaitForSeconds(closeDuration);

        // 3) Hook into Netcode’s scene-loaded callback
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadComplete;

        // 4) Kick off the networked scene load
        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneName,
            LoadSceneMode.Single
        );
    }

    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadMode)
    {
        // Unsubscribe immediately so we only open once
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnLoadComplete;

        // 5) Set the door open
        if (doorAnimator != null)
            doorAnimator.SetBool(isClosedParam, false);
    }
}
