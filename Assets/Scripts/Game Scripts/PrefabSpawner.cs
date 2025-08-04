using UnityEngine;

public class PrefabSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's transform to spawn objects around.")]
    [SerializeField]
    private Transform playerTransform;

    [Tooltip("The prefab to spawn.")]
    [SerializeField]
    private GameObject prefabToSpawn;

    [Header("Spawning Settings")]
    [Tooltip("The time in seconds between each spawn.")]
    [SerializeField]
    private float spawnInterval = 2f;

    [Tooltip("The maximum distance from the player where the prefab can spawn.")]
    [SerializeField]
    private float spawnRadius = 10f;

    private void Start()
    {
        // Check if references are set to avoid errors.
        if (playerTransform == null || prefabToSpawn == null)
        {
            Debug.LogError(
                "Player Transform or Prefab to Spawn is not set in the Spawner. Disabling spawner.",
                this
            );
            this.enabled = false;
            return;
        }

        // Call the SpawnPrefab method every 'spawnInterval' seconds, starting after an initial delay.
        InvokeRepeating(nameof(SpawnPrefab), spawnInterval, spawnInterval);
    }

    private void SpawnPrefab()
    {
        Vector2 randomCirclePoint = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition =
            playerTransform.position + new Vector3(randomCirclePoint.x, randomCirclePoint.y, 0);
        Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerTransform.position, spawnRadius);
    }
}
