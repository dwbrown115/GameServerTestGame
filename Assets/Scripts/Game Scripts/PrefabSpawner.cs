using System.Collections;
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

    public PlayerController2D playerController;

    private void OnEnable()
    {
        CountdownTimer.OnCountdownFinished += StopSpawning;
    }

    private void OnDisable()
    {
        CountdownTimer.OnCountdownFinished -= StopSpawning;
    }

    private void Start()
    {
        if (playerController == null)
        {
            Debug.LogError("PlayerController2D not set on PrefabSpawner.", this);
            this.enabled = false;
            return;
        }
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            Debug.Log("SpawnLoop: Waiting for spawn interval.");
            yield return new WaitForSeconds(spawnInterval);
            Debug.Log("SpawnLoop: Requesting spawn from playerController.");
            playerController.RequestSpawn(spawnRadius);
        }
    }

    public void HandleSpawnResponse(SpawnItemResponse response)
    {
        Debug.Log(
            $"HandleSpawnResponse: Called. Granted={response.Granted}, UniqueId={response.UniqueId}"
        );
        if (response.Granted)
        {
            // The validation is now handled by Collectible.Initialize()
            Vector3 spawnPosition = new Vector3(
                response.SpawnPosition.X,
                response.SpawnPosition.Y,
                0
            );
            Debug.Log($"HandleSpawnResponse: Spawning prefab at {spawnPosition}");
            GameObject newObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

            Collectible collectibleComponent = newObject.GetComponent<Collectible>();
            if (collectibleComponent != null)
            {
                Debug.Log("HandleSpawnResponse: Collectible component found. Initializing.");
                collectibleComponent.Initialize(response.UniqueId, spawnPosition);
            }
            else
            {
                Debug.LogError(
                    "HandleSpawnResponse: Collectible component not found on spawned prefab! Destroying object."
                );
                Destroy(newObject); // Destroy if it's not a collectible
            }

            Debug.Log($"HandleSpawnResponse: Spawned prefab and named it {response.UniqueId}");
        }
        else
        {
            Debug.LogWarning($"HandleSpawnResponse: Spawn not granted. Reason: {response}"); // Assuming a Message field exists
        }
    }

    private void StopSpawning()
    {
        StopAllCoroutines();
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerTransform.position, spawnRadius);
    }
}
