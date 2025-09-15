using UnityEngine;

public class MobSpawner : MonoBehaviour
{
    public GameObject mobPrefab;
    public float spawnInterval = 5f;
    public int maxMobs = 10;
    public float spawnRadius = 10f;

    [Tooltip("Enable to print skip reasons when spawns are blocked.")]
    public bool debugLogs = true;

    [Header("Spawn Target")]
    [Tooltip("Optional explicit target to spawn around; defaults to Player tag if empty.")]
    public Transform spawnAround;

    private float _lastSpawn;
    private bool _stopped;

    private void OnEnable()
    {
        GameOverController.OnCountdownFinished += StopSpawning;
    }

    private void OnDisable()
    {
        GameOverController.OnCountdownFinished -= StopSpawning;
    }

    private void Update()
    {
        if (_stopped)
        {
            if (debugLogs)
                Debug.Log("[MobSpawner] Stopped by game over.", this);
            return;
        }
        if (mobPrefab == null)
        {
            if (debugLogs)
                Debug.LogWarning("[MobSpawner] No mobPrefab assigned.", this);
            return;
        }
        if (Time.time - _lastSpawn < spawnInterval)
        {
            // no log spam; this path is expected most frames
            return;
        }
        int count = transform.childCount;
        if (count >= maxMobs)
        {
            if (debugLogs)
                Debug.Log($"[MobSpawner] Max mobs reached ({count}/{maxMobs}).", this);
            return;
        }

        // Choose center: explicit target, else Player tag, else this spawner
        Vector3 center = transform.position;
        if (spawnAround != null)
        {
            center = spawnAround.position;
        }
        else
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                center = player.transform.position;
        }

        Vector2 offset = Random.insideUnitCircle.normalized * spawnRadius;
        Vector2 spawnPos = (Vector2)center + offset;
        var go = Instantiate(mobPrefab, spawnPos, Quaternion.identity, transform);
        if (debugLogs)
            Debug.Log(
                $"[MobSpawner] Spawned '{go.name}' at {spawnPos} around center {center}.",
                this
            );
        _lastSpawn = Time.time;
    }

    private void StopSpawning()
    {
        _stopped = true;
    }
}
