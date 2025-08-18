using UnityEngine;
using System.Collections;

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
            yield return new WaitForSeconds(spawnInterval);
            playerController.RequestSpawn(spawnRadius);
        }
    }

    public void HandleSpawnResponse(SpawnItemResponse response)
    {
        Debug.Log($"Received Spawn Response: Granted={response.Granted}, UniqueId={response.UniqueId}");
        if (response.Granted)
        {
            bool isValid = IsNumberValid.isValidNumber(response.UniqueId);
            Debug.Log($"UniqueId validation result: {isValid}");
            if (isValid)
            {
                Vector3 spawnPosition = new Vector3(response.SpawnPosition.X, response.SpawnPosition.Y, 0);
                Debug.Log($"Spawning prefab at {spawnPosition}");
                GameObject newObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
                newObject.name = response.UniqueId;
                ValidatedObjectsManager.AddActiveObject(response.UniqueId);
                Debug.Log($"Spawned prefab and named it {response.UniqueId}");
            }
            else
            {
                Debug.LogWarning($"Invalid UniqueId received from server: {response.UniqueId}");
            }
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
