using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;  // for NetworkBehaviour & NetworkVariable

public class DungeonConfigManager : NetworkBehaviour
{
    public static DungeonConfigManager Instance { get; private set; }

    // --- network-synced settings ---
    public NetworkVariable<Vector3Int> ConfiguredSize = new NetworkVariable<Vector3Int>();
    public NetworkVariable<string> ConfiguredSeed = new NetworkVariable<string>();
    public NetworkVariable<int> GenericMinLoot = new NetworkVariable<int>();
    public NetworkVariable<int> GenericMaxLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TreasureMinLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TreasureMaxLoot = new NetworkVariable<int>();
    public NetworkVariable<int> TotalMaxLoot = new NetworkVariable<int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    /// <summary>
    /// Call this on the **server/host** when the player confirms settings in your UI.
    /// It writes into the NetworkVariables and replicates to all clients.
    /// </summary>
    public void SetConfig(Vector3Int size, string seed,
                          int genMin, int genMax, int treMin, int treMax, int totalLoot)
    {
        if (!IsServer) return;  // only the host/server sets
        ConfiguredSize.Value = size;
        ConfiguredSeed.Value = seed;
        GenericMinLoot.Value = genMin;
        GenericMaxLoot.Value = genMax;
        TreasureMinLoot.Value = treMin;
        TreasureMaxLoot.Value = treMax;
        TreasureMaxLoot.Value = treMax;
        TotalMaxLoot.Value = totalLoot;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "PlayScene") return;

        // once synced, apply to the generator & spawner
        // NB: clients will get the same values via NetworkVariables
        var gen = FindObjectOfType<Generator3D>();
        var spawner = FindObjectOfType<LootSpawner>();
        if (gen != null)
        {
            gen.DungeonSize = ConfiguredSize.Value;               // size.Y changes per difficulty :contentReference[oaicite:0]{index=0}
            gen.seedString = ConfiguredSeed.Value;               // same seed on all clients :contentReference[oaicite:1]{index=1}
        }
        if (spawner != null)
        {
            spawner.genericMinLoot = GenericMinLoot.Value;
            spawner.genericMaxLoot = GenericMaxLoot.Value;
            spawner.treasureMinLoot = TreasureMinLoot.Value;
            spawner.treasureMaxLoot = TreasureMaxLoot.Value;
            spawner.totalMaxLoot = TotalMaxLoot.Value;       // adjust global cap :contentReference[oaicite:2]{index=2}
        }
    }
}
