using UnityEngine;
using UnityEngine.UIElements;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [Tooltip("The Main-Menu GO (so we can show it again)")]
    [SerializeField] private GameObject mainMenuGO;

    private UIDocument settingsDoc;
    private VisualElement root;
    private Button backBtn;

    private void Awake()
    {
        // auto-find the UIDocument on this GO
        settingsDoc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        root = settingsDoc.rootVisualElement;
        root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnDisable()
    {
        root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        if (backBtn != null)
            backBtn.clicked -= OnBackClicked;
        backBtn = null;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // only run once per activation
        root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        backBtn = root.Q<Button>("back-button");
        if (backBtn != null)
            backBtn.clicked += OnBackClicked;
        else
            Debug.LogError("[Settings] could not find back-button");
    }

    private void OnBackClicked()
    {
        // hide settings, show main
        gameObject.SetActive(false);
        mainMenuGO.SetActive(true);
    }
}
