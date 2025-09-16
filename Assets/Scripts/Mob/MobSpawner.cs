using UnityEngine;
using UnityEngine.Serialization;

public class MobSpawner : MonoBehaviour
{
    public GameObject mobPrefab;
    public float spawnInterval = 5f;
    public int maxMobs = 10;

    [Header("Spawn Distance Range")]
    [Tooltip(
        "Minimum distance from the target (player by default). Maintained for backward compatibility."
    )]
    [FormerlySerializedAs("minSpawnDistanceFromTarget")]
    [Min(0f)]
    public float minSpawnDistance = 0f;

    [Tooltip(
        "Maximum distance from the target (player by default). Maintained for backward compatibility."
    )]
    [FormerlySerializedAs("spawnRadius")]
    [Min(0f)]
    public float maxSpawnDistance = 10f;

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

        // Compute a spawn offset within the [min, max] annulus around the chosen center
        float inner = Mathf.Max(0f, minSpawnDistance);
        float outer = Mathf.Max(inner, maxSpawnDistance);
        if (outer <= 0f)
        {
            // Fallback to a small outer radius if both are zero (avoid NaNs)
            outer = 1f;
        }
        float r = (Mathf.Approximately(inner, outer)) ? inner : Random.Range(inner, outer);

        Vector2 offset = Random.insideUnitCircle.normalized * r;
        Vector2 spawnPos = (Vector2)center + offset;
        var go = Instantiate(mobPrefab, spawnPos, Quaternion.identity, transform);
        if (debugLogs)
            Debug.Log(
                $"[MobSpawner] Spawned '{go.name}' at {spawnPos} around center {center} (r={r:F2}, range=[{inner:F2},{outer:F2}]).",
                this
            );
        _lastSpawn = Time.time;
    }

    private void StopSpawning()
    {
        _stopped = true;
    }

    private void OnDrawGizmosSelected()
    {
        // Determine center: explicit target, else Player tag, else this spawner
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

        float inner = Mathf.Max(0f, minSpawnDistance);
        float outer = Mathf.Max(inner, maxSpawnDistance);

        // Draw inner (deadzone) in red
        if (inner > 0f)
        {
            var prev = Gizmos.color;
            Gizmos.color = new Color(1f, 0f, 0f, 0.9f);
            Gizmos.DrawWireSphere(center, inner);
            Gizmos.color = prev;
        }

        // Draw outer (max range) in green
        if (outer > 0f)
        {
            var prev = Gizmos.color;
            Gizmos.color = new Color(0f, 1f, 0f, 0.9f);
            Gizmos.DrawWireSphere(center, outer);
            Gizmos.color = prev;
        }
    }
}
