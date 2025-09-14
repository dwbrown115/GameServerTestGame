using UnityEngine;

public class MobSpawner : MonoBehaviour
{
    public GameObject mobPrefab;
    public float spawnInterval = 5f;
    public int maxMobs = 10;
    public float spawnRadius = 10f;

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
            return;
        if (mobPrefab == null)
            return;
        if (Time.time - _lastSpawn < spawnInterval)
            return;
        int count = transform.childCount;
        if (count >= maxMobs)
            return;

        Vector2 offset = Random.insideUnitCircle.normalized * spawnRadius;
        var go = Instantiate(
            mobPrefab,
            (Vector2)transform.position + offset,
            Quaternion.identity,
            transform
        );
        _lastSpawn = Time.time;
    }

    private void StopSpawning()
    {
        _stopped = true;
    }
}
