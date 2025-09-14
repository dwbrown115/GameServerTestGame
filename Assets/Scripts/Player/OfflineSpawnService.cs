using System;
using System.Collections;
using UnityEngine;

// Mimics the server spawn flow locally for offline gameplay testing
public class OfflineSpawnService : MonoBehaviour
{
    [SerializeField]
    private PrefabSpawner spawner; // Assign in scene

    [SerializeField]
    private float minDelay = 0.05f;

    [SerializeField]
    private float maxDelay = 0.2f;

    private System.Random _rng = new System.Random();

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<PrefabSpawner>();
        }
    }

    public void RequestSpawn(SpawnItemRequest request)
    {
        // Simulate server processing delay then invoke spawner.HandleSpawnResponse
        StartCoroutine(CoRespond(request));
    }

    private IEnumerator CoRespond(SpawnItemRequest request)
    {
        float delay = Mathf.Lerp(minDelay, maxDelay, (float)_rng.NextDouble());
        yield return new WaitForSeconds(delay);

        Vector2 center = new Vector2(request.PlayerPosition.X, request.PlayerPosition.Y);
        float r = request.SpawnRadius;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * r;
        var response = new SpawnItemResponse
        {
            ResponseType = "spawn_request_response",
            SpawnPosition = new Position { X = center.x + offset.x, Y = center.y + offset.y },
            UniqueId = GenerateValidNumericId(),
            SessionId = request.SessionId,
            Granted = true,
        };
        if (spawner != null)
        {
            spawner.HandleSpawnResponse(response);
        }
    }

    private string GenerateValidNumericId()
    {
        // Make a 9-digit base and append checksum mod 10
        int baseLen = 9;
        int sum = 0;
        char[] digits = new char[baseLen + 1];
        for (int i = 0; i < baseLen; i++)
        {
            int d = _rng.Next(0, 10);
            digits[i] = (char)('0' + d);
            sum += d;
        }
        int checksum = sum % 10;
        digits[baseLen] = (char)('0' + checksum);
        return new string(digits);
    }
}
