using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Collections;
using System.Linq;

public class DungeonSettingsUI : NetworkBehaviour
{
    [Header("Door Animation (optional)")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string isClosedParam = "IsClosed";
    [SerializeField] private float closeDuration = 1f;

    [Header("UI References (assign in inspector)")]
    [SerializeField] private TMP_Dropdown difficultyDropdown;
    [SerializeField] private TMP_InputField seedInputField;
    [SerializeField] private Button randomSeedButton;
    [SerializeField] private Button confirmButton;
    private int genMin, genMax, treMin, treMax, totalLoot;
    [Header("Base Dungeon Dimensions")]
    [SerializeField] private int baseSizeX = 20;
    [SerializeField] private int baseSizeZ = 20;

    [Header("Play Scene Name (Build Settings)")]
    [SerializeField] private string playSceneName = "PlayScene";
    [Header("Mode Selection")]
    [SerializeField] private ToggleGroup modeToggleGroup;
    [SerializeField] private List<string> playSceneNames = new List<string> { "Lob", "Lob 1", "Lob 2" };
    // --- Networked config variables ---
    public NetworkVariable<Vector3Int> ConfiguredSize =
        new NetworkVariable<Vector3Int>();
    public NetworkVariable<FixedString128Bytes> ConfiguredSeed =
        new NetworkVariable<FixedString128Bytes>(new FixedString128Bytes());
    public NetworkVariable<int> GenericMinLoot = new NetworkVariable<int>();
    public NetworkVariable<int> GenericMaxLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TreasureMinLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TreasureMaxLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TotalMaxLoot = new NetworkVariable<int>();
    public NetworkVariable<int> DifficultyIndex =
        new NetworkVariable<int>(0);
    public NetworkVariable<float> SpawnLightChance = new NetworkVariable<float>();
    void Awake()
    {
        if (doorAnimator == null)
        {
            var doorGO = GameObject.Find("DoorTrain");
            if (doorGO != null)
                doorAnimator = doorGO.GetComponent<Animator>();
            else
                Debug.LogWarning("DungeonSettingsUI: no GameObject named 'DoorTrain' found.");
        }
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Populate difficulty dropdown
        difficultyDropdown.ClearOptions();
        var labels = new List<string> { "Very Easy", "Easy", "Medium", "Hard", "Insane", "Nightmare" };
        difficultyDropdown.AddOptions(labels);
        difficultyDropdown.RefreshShownValue();

        // Seed field: numeric only, max-6 chars, pad on end-edit
        seedInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        seedInputField.characterLimit = 6;
        seedInputField.onValidateInput += (s, i, c) => char.IsDigit(c) ? c : '\0';
        seedInputField.onEndEdit.AddListener(text => seedInputField.text = text.PadLeft(6, '0'));

        // Buttons
        randomSeedButton.onClick.AddListener(() =>
            seedInputField.text = Random.Range(0, 999_999).ToString("D6")
        );
        confirmButton.onClick.AddListener(OnConfirmSettings);
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != playSceneName) return;

        // once we land in PlayScene, find the spawner…
        var spawner = FindObjectOfType<LootSpawner>();
        if (spawner != null)
        {
            spawner.genericMinLoot = genMin;
            spawner.genericMaxLoot = genMax;
            spawner.treasureMinLoot = treMin;
            spawner.treasureMaxLoot = treMax;
            spawner.totalMaxLoot = totalLoot;
            Debug.Log($"[DungeonSettingsUI] Applied loot caps: G[{genMin}-{genMax}] T[{treMin}-{treMax}] Total={totalLoot}");
        }
    }
    public override void OnNetworkSpawn()
    {
        // Listen for every client when the networked scene load completes
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnLoadComplete;
    }

    private void OnConfirmSettings()
    {
        // 1) Gather & clamp values
        var seed = seedInputField.text.Trim().PadLeft(6, '0');
        int diff = difficultyDropdown.value;

        int rawY = diff == 0 ? 2
                 : diff == 1 ? 3
                 : diff == 2 ? 5
                 : diff == 3 ? 7
                 : diff == 4 ? 9
                 : 11;
        int sizeY = Mathf.Max(3, rawY);

        int genMin = sizeY == 3 ? 1 : 2;
        int genMax = sizeY == 3 ? 2 : (diff <= 2 ? 4 : 6);
        int treMin = sizeY == 3 ? 3 : 5;
        int treMax = sizeY == 3 ? 5 : (diff <= 2 ? 8 : 12);
        int total = sizeY * 10;

        float lightChance = Mathf.Lerp(0.8f, 0.15f, diff / 5f);

        this.genMin = genMin;
        this.genMax = genMax;
        this.treMin = treMin;
        this.treMax = treMax;
        this.totalLoot = total;
        // 2) Start the close-and-load sequence
        string sceneToLoad = playSceneName;
        var activeToggle = modeToggleGroup.ActiveToggles().FirstOrDefault();
        if (activeToggle != null)
        {
            Toggle[] toggles = modeToggleGroup.GetComponentsInChildren<Toggle>();
            int index = System.Array.IndexOf(toggles, activeToggle);
            if (index >= 0 && index < playSceneNames.Count)
                sceneToLoad = playSceneNames[index];
        }

        // 3) Start loading with the chosen scene
        StartCoroutine(CloseAndLoadRoutine(
            new Vector3Int(baseSizeX, sizeY, baseSizeZ),
            seed,
            genMin, genMax,
            treMin, treMax,
            total,
            diff,
            lightChance,
            sceneToLoad
        ));
    }

    private IEnumerator CloseAndLoadRoutine(
        Vector3Int size,
        string seed,
        int genMin, int genMax,
        int treMin, int treMax,
        int totalLoot, int diff,
        float lightChance,
        string sceneName
    )
    {
        // Play door-closing animation locally
        if (doorAnimator != null)
            doorAnimator.SetBool(isClosedParam, true);

        yield return new WaitForSeconds(closeDuration);

        // Host: apply & load directly
        if (IsServer)
        {
            ApplyConfig(size, seed, genMin, genMax, treMin, treMax, totalLoot, lightChance);
            DifficultyIndex.Value = diff;
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
        // Client: ask host
        else if (IsClient)
        {
            RequestSetConfigAndLoadServerRpc(
                size, seed, genMin, genMax, treMin, treMax, totalLoot, diff,lightChance, sceneName
            );
        }
        else
        {
            Debug.LogWarning("No NetworkManager found to load dungeon.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetConfigAndLoadServerRpc(
        Vector3Int size,
        string seed,
        int genMin, int genMax,
        int treMin, int treMax,
        int totalLoot, int diff,float lightChance,
        string sceneName
    )
    {
        ApplyConfig(size, seed, genMin, genMax, treMin, treMax, totalLoot, lightChance);
        DifficultyIndex.Value = diff;
        NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private void ApplyConfig(
        Vector3Int size,
        string seed,
        int genMin, int genMax,
        int treMin, int treMax,
        int totalLoot, float lightChance
    )
    {
        ConfiguredSize.Value = size;
        ConfiguredSeed.Value = new FixedString128Bytes(seed);
        GenericMinLoot.Value = genMin;
        GenericMaxLoot.Value = genMax;
        TreasureMinLoot.Value = treMin;
        TreasureMaxLoot.Value = treMax;
        TotalMaxLoot.Value = totalLoot;
        SpawnLightChance.Value = lightChance;
    }

    private void OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // Only handle our local client and only the PlayScene
        if (clientId != NetworkManager.Singleton.LocalClientId)
            return;

        // …and only when one of our play scenes is loaded
        if (playSceneNames == null || !playSceneNames.Contains(sceneName))
            return;

        // 1) Apply config to Generator3D & LootSpawner
        var gen = FindObjectOfType<Generator3D>();
        var spawner = FindObjectOfType<LootSpawner>();
        var lit = FindObjectOfType<DecorationPlacer>();
        if (gen != null)
        {
            gen.DungeonSize = ConfiguredSize.Value;
            gen.seedString = ConfiguredSeed.Value.ToString();

        }

        if (spawner != null)
        {
            spawner.genericMinLoot = GenericMinLoot.Value;
            spawner.genericMaxLoot = GenericMaxLoot.Value;
            spawner.treasureMinLoot = TreasureMinLoot.Value;
            spawner.treasureMaxLoot = TreasureMaxLoot.Value;
            spawner.totalMaxLoot = TotalMaxLoot.Value;
        }

        if ( lit != null)
        {
            lit.lightChance = SpawnLightChance.Value;
        }
        var doorGO = GameObject.Find("DoorTrain");
        if (doorGO != null)
            doorAnimator = doorGO.GetComponent<Animator>();
        else
            Debug.LogWarning("DungeonSettingsUI: no 'DoorTrain' found after load.");

        // 2) Open the door animation
        if (doorAnimator != null)
            doorAnimator.SetBool(isClosedParam, false);
        

    }
    
}
