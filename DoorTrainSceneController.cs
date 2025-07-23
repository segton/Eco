using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Animator))]
public class DoorTrainSceneController : MonoBehaviour
{
    [Tooltip("Name of the Scene where the door should start closed")]
    [SerializeField] private string closedSceneName = "Starty";

    [Tooltip("Animator bool parameter to close the door")]
    [SerializeField] private string isClosedParam = "IsClosed";

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // Fire once for the scene we're already in
        CheckAndApply(SceneManager.GetActiveScene());

        // Subscribe for any future loads
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndApply(scene);
    }

    private void CheckAndApply(Scene scene)
    {
        bool shouldBeClosed = scene.name == closedSceneName;
        animator.SetBool(isClosedParam, shouldBeClosed);
    }
}
