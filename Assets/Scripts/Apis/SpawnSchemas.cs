using System;
using Newtonsoft.Json;

[Serializable]
public class Coordinates
{
    [JsonProperty("x")]
    public float X { get; set; }

    [JsonProperty("y")]
    public float Y { get; set; }
}

[Serializable]
public class ValidatedObject
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("clientSpawnedTime")]
    public DateTime ClientSpawnedTime { get; set; }

    [JsonProperty("claimedTime")]
    public DateTime? ClaimedTime { get; set; }

    [JsonProperty("coordinates")]
    public Coordinates Coordinates { get; set; }
}

[Serializable]
public class ClaimObjectRequest
{
    [JsonProperty("request_type")]
    public string RequestType { get; set; }

    [JsonProperty("sessionId")]
    public string SessionId { get; set; }

    [JsonProperty("claimedObject")]
    public ValidatedObject ClaimedObject { get; set; }
}

public class SpawnItemRequest
{
    [JsonProperty("request_type")]
    public string RequestType { get; set; }

    [JsonProperty("session_id")]
    public string SessionId { get; set; }

    [JsonProperty("player_position")]
    public Position PlayerPosition { get; set; }

    [JsonProperty("spawn_attempt_timestamp")]
    public DateTime SpawnAttemptTimestamp { get; set; }

    [JsonProperty("spawn_radius")]
    public float SpawnRadius { get; set; }
}

public class SpawnItemResponse
{
    [JsonProperty("response_type")]
    public string ResponseType { get; set; }

    [JsonProperty("spawn_position")]
    public Position SpawnPosition { get; set; }

    [JsonProperty("unique_id")]
    public string UniqueId { get; set; }

    [JsonProperty("session_id")]
    public string SessionId { get; set; }

    [JsonProperty("granted")]
    public bool Granted { get; set; }
}

public class Position
{
    [JsonProperty("X")]
    public float X { get; set; }

    [JsonProperty("Y")]
    public float Y { get; set; }
}

[Serializable]
public class ClaimObjectResponse
{
    [JsonProperty("response_type")]
    public string ResponseType { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }
}
