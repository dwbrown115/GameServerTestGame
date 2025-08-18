using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class ValidatedObjectsManager
{
    private static readonly string FilePath = Path.Combine(
        Application.dataPath,
        "_DebugTokens",
        "validatedObjects.json"
    );

    private class ValidatedObject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("spawnedTime")]
        public DateTime SpawnedTime { get; set; }

        [JsonProperty("claimedTime")]
        public DateTime? ClaimedTime { get; set; }
    }

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
        Debug.Log($"ValidatedObjectsManager: CreateOrResetFile called with sessionId: '{sessionId}'");

        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogError("ValidatedObjectsManager: CreateOrResetFile called with a null or empty sessionId.");
            return;
        }

        try
        {
            // Ensure the directory exists
            string directoryPath = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directoryPath))
            {
                Debug.Log($"ValidatedObjectsManager: Creating directory at {directoryPath}");
                Directory.CreateDirectory(directoryPath);
            }

            if (File.Exists(FilePath))
            {
                Debug.Log($"ValidatedObjectsManager: Deleting existing file at {FilePath}");
                File.Delete(FilePath);
            }

            var initialData = new ValidatedObjectsData
            {
                SessionId = sessionId,
                SpawnedObjectsNumber = 0,
                ClaimedObjectsNumber = 0,
            };
            string json = JsonConvert.SerializeObject(initialData, Formatting.Indented);
            Debug.Log($"ValidatedObjectsManager: Writing new file to {FilePath} with content: {json}");
            File.WriteAllText(FilePath, json);
            Debug.Log($"ValidatedObjectsManager: validatedObjects.json created successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"ValidatedObjectsManager: An error occurred in CreateOrResetFile: {ex.ToString()}");
        }
    }

    public static void DeleteFile()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public static void AddActiveObject(string objectId)
    {
        if (!File.Exists(FilePath))
            return;

        string json = File.ReadAllText(FilePath);
        var data = JsonConvert.DeserializeObject<ValidatedObjectsData>(json);

        if (data.ValidatedObjects.All(o => o.Id != objectId))
        {
            data.ValidatedObjects.Add(
                new ValidatedObject { Id = objectId, SpawnedTime = DateTime.UtcNow }
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
        }
    }
}