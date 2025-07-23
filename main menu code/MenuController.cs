using UnityEngine;
using UnityEngine.SceneManagement;

namespace ECOscavengers.UI
{
    public class MenuController : MonoBehaviour
    {
        // Use a private string field with [SerializeField] to expose it in the
        // Inspector. This is safer than a public field and allows you to
        // easily change the scene name without editing the script.
        [Tooltip("The name of the scene to load when the play button is clicked.")]
        [SerializeField] private string sceneToLoad = "Join";

        public void PlayGame() // Renamed for clarity from "PlayedGame"
        {
            // It's helpful to log which scene you are trying to load.
            Debug.Log($"Attempting to load scene: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad);
        }

        public void QuitGame()
        {
            Debug.Log("Quitting application");

            // Application.Quit() does not work in the Unity Editor.
            // This conditional compilation directive adds code that will
            // stop play mode when you are in the editor.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}