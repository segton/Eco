using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class DungeonEnemySpawner : NetworkBehaviour
{
    [Header("Enemy Prefabs")]
    [Tooltip("Regular foes")]
    [SerializeField] private List<GameObject> normalEnemies;
    [Tooltip("Tougher foes (spawn in boss rooms)")]
    [SerializeField] private List<GameObject> bossEnemies;

    [Header("Spawn Timing (seconds)")]
    [Tooltip("Slowest spawn at diff=0")]
    [SerializeField] private float maxInterval = 300f;
    [Tooltip("Fastest spawn at diff=5")]
    [SerializeField] private float minInterval = 120f;

    [Header("Scene Names")]
    [Tooltip("All play/dungeon scenes that should trigger enemy spawns.")]
    [SerializeField] private List<string> playSceneNames = new List<string>();


    private TrainSafetyManager trainManager;
    private DungeonSettingsUI ui;
    private RoomSetup roomSetup;
    private GameObject localPlayer;
    private float lastSpawnTime;
    private List<NetworkObject> spawnedEnemies = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // grab references
        trainManager = GetComponent<TrainSafetyManager>();
        ui = FindObjectOfType<DungeonSettingsUI>();
        localPlayer = GameObject.FindGameObjectWithTag("Player");

        // watch for networked scene loads
        NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
        // watch for Unity scene unloads
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // only run on host, when PlayScene is loaded
        if (clientId != NetworkManager.ServerClientId || !playSceneNames.Contains(sceneName))
            return;

        // reset cleanup
        spawnedEnemies.Clear();

        // find RoomSetup once it exists
        StartCoroutine(InitWhenRoomsReady());
    }

    private IEnumerator InitWhenRoomsReady()
    {
        // disable until ready
        enabled = false;

        // wait for RoomSetup to build rooms[]
        while ((roomSetup = FindObjectOfType<RoomSetup>()) == null ||
               roomSetup.rooms == null ||
               roomSetup.rooms.Length == 0)
            yield return null;

        // sync our spawn clock to the train timer
        lastSpawnTime = trainManager.ElapsedTime;

        // now start spawning in Update()
        enabled = true;
    }

    private void Update()
    {
        if (!IsServer) return;

        // compute spawn interval based on difficulty 0..5
        int diff = ui != null ? ui.DifficultyIndex.Value : 0;
        float t = Mathf.Clamp01(diff / 5f);
        float interval = Mathf.Lerp(maxInterval, minInterval, t);

        // only proceed when train timer passes the next tick
        float elapsed = trainManager.ElapsedTime;
        if (elapsed - lastSpawnTime < interval) return;
        lastSpawnTime = elapsed;

        // do the actual spawn
        SpawnOne(t);
    }

    private void SpawnOne(float unused)
    {
        if (roomSetup == null) return;

        // recompute diffT here
        int diff = ui != null ? ui.DifficultyIndex.Value : 0;
        float diffT = Mathf.Clamp01(diff / 5f);

        // **NEW**: bossChance ramps 10% 75% instead of 0100%
        float bossChance = Mathf.Lerp(0.10f, 0.75f, diffT);
        bool spawnBoss = Random.value < bossChance;

        // collect boss vs normal rooms
        var bossRooms = roomSetup.rooms.Where(r => r.roomType == RoomType.Boss).ToList();
        var normalRooms = roomSetup.rooms.Where(r => r.roomType != RoomType.Boss).ToList();

        var candidates = (spawnBoss ? bossRooms : normalRooms);
        if (candidates.Count == 0)
            candidates = (spawnBoss ? normalRooms : bossRooms);
        if (candidates.Count == 0) return;

        if (localPlayer != null)
        {
            Vector3 p = localPlayer.transform.position;
            candidates = candidates
                .Where(r =>
                {
                    var col = r.GetComponent<Collider>();
                    return col == null || !col.bounds.Contains(p);
                })
                .ToList();
            if (candidates.Count == 0) return;
        }

        // pick a room and prefab
        var room = candidates[Random.Range(0, candidates.Count)];
        var prefabList = spawnBoss ? bossEnemies : normalEnemies;
        if (prefabList == null || prefabList.Count == 0)
            prefabList = spawnBoss ? normalEnemies : bossEnemies;
        if (prefabList == null || prefabList.Count == 0) return;

        var prefab = prefabList[Random.Range(0, prefabList.Count)];
        var go = Instantiate(prefab, room.transform.position, Quaternion.identity);
        if (go.TryGetComponent<NetworkObject>(out var net))
        {
            net.Spawn();
            spawnedEnemies.Add(net);
        }

        Debug.Log($"[Spawner] Spawned {(spawnBoss ? "BOSS" : "NORMAL")} in {room.roomType} (bossChance={bossChance:P0})");
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // when any Unity scene unloads, if it was PlayScene we should clean up
        if (!playSceneNames.Contains(scene.name)) return;

        foreach (var net in spawnedEnemies)
        {
            if (net != null)
            {
                if (net.IsSpawned) net.Despawn(true);
                else Destroy(net.gameObject);
            }
        }
        spawnedEnemies.Clear();

        // disable until next PlayScene load
        enabled = false;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
}
