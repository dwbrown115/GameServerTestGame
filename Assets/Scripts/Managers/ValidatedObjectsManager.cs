using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class ValidatedObjectsManager
{
    private static PlayerWebSocketClient _webSocketClient;

    public static void Initialize(PlayerWebSocketClient client)
    {
        _webSocketClient = client;
    }

    private static readonly string FilePath = Path.Combine(
        Application.dataPath,
        "_DebugTokens",
        "validatedObjects.json"
    );

    private class ValidatedObjectsData
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("validatedObjects")]
        public List<ValidatedObject> ValidatedObjects { get; set; } = new List<ValidatedObject>();

        [JsonProperty("spawnedObjectsNumber")]
        public int SpawnedObjectsNumber { get; set; }

        [JsonProperty("claimedObjectsNumber")]
        public int ClaimedObjectsNumber { get; set; }
    }

    public static void CreateOrResetFile(string sessionId)
    {
        Debug.Log(
            $"ValidatedObjectsManager: CreateOrResetFile called with sessionId: '{sessionId}'"
        );

        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError(
                "ValidatedObjectsManager: CreateOrResetFile called with a null or empty sessionId."
            );
            return;
        }

        try
        {
            string directoryPath = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            var initialData = new ValidatedObjectsData
            {
                SessionId = sessionId,
                SpawnedObjectsNumber = 0,
                ClaimedObjectsNumber = 0,
            };
            string json = JsonConvert.SerializeObject(initialData, Formatting.Indented);
            File.WriteAllText(FilePath, json);
            Debug.Log($"ValidatedObjectsManager: validatedObjects.json created successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"ValidatedObjectsManager: An error occurred in CreateOrResetFile: {ex.ToString()}"
            );
        }
    }

    public static void DeleteFile()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public static void AddActiveObject(string objectId, Vector3 position)
    {
        if (!File.Exists(FilePath))
            return;

        string json = File.ReadAllText(FilePath);
        var data = JsonConvert.DeserializeObject<ValidatedObjectsData>(json);

        if (data.ValidatedObjects.All(o => o.Id != objectId))
        {
            data.ValidatedObjects.Add(
                new ValidatedObject
                {
                    Id = objectId,
                    ClientSpawnedTime = DateTime.UtcNow,
                    Coordinates = new Coordinates { X = position.x, Y = position.y },
                }
            );
            data.SpawnedObjectsNumber++;
            string updatedJson = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(FilePath, updatedJson);
        }
    }

    public static void DestroyObject(string objectId)
    {
        if (!File.Exists(FilePath))
            return;

        string json = File.ReadAllText(FilePath);
        var data = JsonConvert.DeserializeObject<ValidatedObjectsData>(json);

        var obj = data.ValidatedObjects.FirstOrDefault(o => o.Id == objectId);
        if (obj != null && obj.ClaimedTime == null)
        {
            obj.ClaimedTime = DateTime.UtcNow;
            data.ClaimedObjectsNumber++;
            string updatedJson = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(FilePath, updatedJson);

            Debug.Log(
                $"ValidatedObjectsManager: Object with ID '{obj.Id}' claimed at {obj.ClaimedTime}."
            );

            if (_webSocketClient != null)
            {
                var request = new ClaimObjectRequest
                {
                    RequestType = "object_claimed_request",
                    SessionId = data.SessionId,
                    ClaimedObject = obj,
                };
                _webSocketClient.SendClaimObjectRequestAsync(request);
            }
            else
            {
                Debug.LogWarning(
                    "ValidatedObjectsManager: PlayerWebSocketClient is not initialized. Cannot send claim message."
                );
            }
        }
    }

    public static HashSet<string> GetValidatedObjectIds()
    {
        if (!File.Exists(FilePath))
        {
            return new HashSet<string>();
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            var data = JsonConvert.DeserializeObject<ValidatedObjectsData>(json);
            return new HashSet<string>(data.ValidatedObjects.Select(o => o.Id));
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"ValidatedObjectsManager: Error reading validatedObjects.json: {ex.Message}"
            );
            return new HashSet<string>();
        }
    }

    public static bool IsObjectClaimed(string objectId)
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            var data = JsonConvert.DeserializeObject<ValidatedObjectsData>(json);
            var obj = data.ValidatedObjects.FirstOrDefault(o => o.Id == objectId);
            return obj != null && obj.ClaimedTime != null;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"ValidatedObjectsManager: Error checking if object is claimed: {ex.Message}"
            );
            return false;
        }
    }
}
