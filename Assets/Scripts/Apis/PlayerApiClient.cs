using System;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class PlayerResponse
{
    public string userId;
    public string userName;
}

public class PlayerApiClient : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("The base URL of your game server, e.g., https://localhost:7123")]
    private string apiBaseUrl = "https://localhost:7123";

    public Coroutine GetPlayerData(Action<PlayerResponse> onSuccess, Action<string> onError)
    {
        return StartCoroutine(
            RequestPlayerData(PlayerManager.Instance.GetUserId(), onSuccess, onError)
        );
    }

    private IEnumerator RequestPlayerData(
        string userId,
        Action<PlayerResponse> onSuccess,
        Action<string> onError
    )
    {
        // Construct the full URL for the API endpoint
        string url = $"{apiBaseUrl}/player/{userId}";
        Debug.Log($"Sending request to: {url}");

        // Create a UnityWebRequest object for a GET request
        var headers = new System.Collections.Generic.Dictionary<string, string>();
        string token = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(token))
        {
            headers["Authorization"] = "Bearer " + token;
        }

        yield return Net.HttpRequest.Send(
            "GET",
            url,
            resp =>
            {
                if (resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.Log("Request successful!");
                    string jsonResponse = resp.body;
                    Debug.Log($"Response JSON: {jsonResponse}");
                    PlayerResponse player = JsonConvert.DeserializeObject<PlayerResponse>(
                        jsonResponse
                    );
                    Debug.Log(
                        $"<color=green>Successfully fetched player: {player.userName} (ID: {player.userId})</color>"
                    );
                    onSuccess?.Invoke(player);
                }
                else
                {
                    Debug.LogError($"<color=red>Error fetching player data: {resp.error}</color>");
                    if (resp.statusCode == 404)
                    {
                        Debug.LogError("Player not found on the server (404).");
                    }
                    onError?.Invoke(resp.error);
                }
            },
            null,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );
    }

    // You can use this for quick testing in the editor
    [ContextMenu("Test Fetch Player Data")]
    private void TestFetch()
    {
        // This allows you to right-click the component in the Inspector and run the test
        if (Application.isPlaying)
        {
            // Use the version with callbacks for better test feedback
            GetPlayerData(
                onSuccess: (player) =>
                    Debug.Log($"[Test] Successfully fetched player: {player.userName}"),
                onError: (error) => Debug.LogError($"[Test] Failed to fetch player: {error}")
            );
        }
        else
        {
            Debug.LogWarning("You must be in Play Mode to test API calls.");
        }
    }
}
