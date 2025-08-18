using System;
using Newtonsoft.Json;

public class SpawnItemRequest
{
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
    [JsonProperty("SpawnPosition")]
    public Position SpawnPosition { get; set; }

    [JsonProperty("UniqueId")]
    public string UniqueId { get; set; }

    [JsonProperty("SessionId")]
    public string SessionId { get; set; }

    [JsonProperty("Granted")]
    public bool Granted { get; set; }
}

public class Position
{
    [JsonProperty("X")]
    public float X { get; set; }

    [JsonProperty("Y")]
    public float Y { get; set; }
}
