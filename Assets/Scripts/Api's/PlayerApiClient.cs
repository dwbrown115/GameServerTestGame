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
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Player data requests require an authorization token
            string token = JwtManager.Instance.GetJwt();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            // Send the request and wait for a response
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Request was successful
                Debug.Log("Request successful!");

                // Get the raw JSON response text
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"Response JSON: {jsonResponse}");

                // Use Newtonsoft.Json to parse the JSON into our PlayerResponse object
                PlayerResponse player = JsonConvert.DeserializeObject<PlayerResponse>(jsonResponse);

                // Now you can use the player data
                Debug.Log(
                    $"<color=green>Successfully fetched player: {player.userName} (ID: {player.userId})</color>"
                );

                onSuccess?.Invoke(player);
            }
            else
            {
                // An error occurred
                Debug.LogError($"<color=red>Error fetching player data: {request.error}</color>");

                // You can also check the HTTP status code
                if (request.responseCode == 404)
                {
                    Debug.LogError("Player not found on the server (404).");
                }
                onError?.Invoke(request.error);
            }
        }
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
