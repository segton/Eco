using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement mainMenuContainer;
    private VisualElement settingsMenuContainer;
    private Button startButton;
    private Button settingButton;
    private Button exitButton;
    private Button backButton;

    void Awake()
    {
        var root = uiDocument.rootVisualElement;

        // grab both containers
        mainMenuContainer = root.Q<VisualElement>("main-menu-container");
        settingsMenuContainer = root.Q<VisualElement>("settings-menu-container");

        // grab all buttons
        startButton = root.Q<Button>("start-button");
        settingButton = root.Q<Button>("setting-button");
        exitButton = root.Q<Button>("exit-button");
        backButton = root.Q<Button>("back-button");

        // wire callbacks
        startButton.clicked += OnStartClicked;
        settingButton.clicked += OnSettingClicked;
        exitButton.clicked += OnExitClicked;
        backButton.clicked += OnBackClicked;
    }

    void OnDestroy()
    {
        // unsubscribe on teardown
        startButton.clicked -= OnStartClicked;
        settingButton.clicked -= OnSettingClicked;
        exitButton.clicked -= OnExitClicked;
        backButton.clicked -= OnBackClicked;
    }

    void Start()
    {
        ShowMainMenu();
    }

    private void OnStartClicked()
    {
        SceneManager.LoadScene("join");
    }

    private void OnSettingClicked()
    {
        mainMenuContainer.style.display = DisplayStyle.None;
        settingsMenuContainer.style.display = DisplayStyle.Flex;
    }

    private void OnBackClicked()
    {
        settingsMenuContainer.style.display = DisplayStyle.None;
        mainMenuContainer.style.display = DisplayStyle.Flex;
    }

    private void OnExitClicked()
    {
        Application.Quit();
    }

    private void ShowMainMenu()
    {
        mainMenuContainer.style.display = DisplayStyle.Flex;
        settingsMenuContainer.style.display = DisplayStyle.None;
    }
}
