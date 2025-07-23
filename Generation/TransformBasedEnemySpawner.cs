using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TransformBasedEnemySpawner : NetworkBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private List<Transform> spawnPoints = new();

    [Header("Enemy Prefabs")]
    [SerializeField] private List<GameObject> normalEnemies = new();
    [SerializeField] private List<GameObject> bossEnemies = new();

    [Header("Spawn Timing (seconds)")]
    [SerializeField] private float maxInterval = 300f;
    [SerializeField] private float minInterval = 120f;

    private DungeonSettingsUI ui;
    private TrainSafetyManager trainManager;
    private float lastSpawnTime;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Seed the timer now; the real references will get filled in Update()
        lastSpawnTime = Time.time;
    }

    private void Update()
    {
        if (!IsServer || spawnPoints.Count == 0)
            return;

        // 1) Ensure we have our refs
        if (ui == null)
            ui = UnityEngine.Object.FindAnyObjectByType<DungeonSettingsUI>();
        if (trainManager == null)
            trainManager = UnityEngine.Object.FindAnyObjectByType<TrainSafetyManager>();

        // 2) Determine difficulty t in [0…1]
        int diff = ui != null ? ui.DifficultyIndex.Value : 0;
        float t = Mathf.Clamp01(diff / 5f);

        // 3) Compute interval based on difficulty
        float interval = Mathf.Lerp(maxInterval, minInterval, t);

        // 4) Decide if it's time to spawn
        float now = trainManager != null ? trainManager.ElapsedTime : Time.time;
        if (now - lastSpawnTime < interval)
            return;

        lastSpawnTime = now;
        SpawnOne(t);
    }

    private void SpawnOne(float diffT)
    {
        // boss chance from 10%  75%
        bool spawnBoss = Random.value < Mathf.Lerp(0.10f, 0.75f, diffT);

        var list = (spawnBoss && bossEnemies.Count > 0)
            ? bossEnemies
            : normalEnemies;
        if (list.Count == 0) return;

        // pick random enemy & spawn point
        var prefab = list[Random.Range(0, list.Count)];
        var pt = spawnPoints[Random.Range(0, spawnPoints.Count)];

        var go = Instantiate(prefab, pt.position, pt.rotation);
        if (go.TryGetComponent<NetworkObject>(out var net))
            net.Spawn();
    }
}
