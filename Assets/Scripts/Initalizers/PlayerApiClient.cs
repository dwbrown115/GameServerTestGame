using System;
using System.Collections;
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
    [Tooltip("The base URL of your game server, e.g., http://localhost:5140")]
    [SerializeField]
    private string apiBaseUrl = "http://localhost:5140";

    public void GetPlayerData()
    {
        // Start the coroutine to handle the asynchronous web request
        StartCoroutine(RequestPlayerData(PlayerManager.Instance.GetUserId(), null));
    }

    public Coroutine GetPlayerData(Action<bool> onCompleted)
    {
        return StartCoroutine(RequestPlayerData(PlayerManager.Instance.GetUserId(), onCompleted));
    }

    private IEnumerator RequestPlayerData(string userId, Action<bool> onCompleted)
    {
        // Construct the full URL for the API endpoint
        string url = $"{apiBaseUrl}/player/{userId}";
        Debug.Log($"Sending request to: {url}");
        bool success = false;

        // Create a UnityWebRequest object for a GET request
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // Send the request and wait for a response
            yield return request.SendWebRequest();

            success = request.result == UnityWebRequest.Result.Success;

            if (success)
            {
                // Request was successful
                Debug.Log("Request successful!");

                // Get the raw JSON response text
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"Response JSON: {jsonResponse}");

                // Use JsonUtility to parse the JSON into our PlayerResponse object
                PlayerResponse player = JsonUtility.FromJson<PlayerResponse>(jsonResponse);

                // Now you can use the player data
                Debug.Log(
                    $"<color=green>Successfully fetched player: {player.userName} (ID: {player.userId})</color>"
                );

                PlayerManager.Instance.SetPlayerData(player.userId, player.userName);
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
            }

            onCompleted?.Invoke(success);
        }
    }

    // You can use this for quick testing in the editor
    [ContextMenu("Test Fetch Player Data")]
    private void TestFetch()
    {
        // This allows you to right-click the component in the Inspector and run the test
        if (Application.isPlaying)
        {
            GetPlayerData();
        }
        else
        {
            Debug.LogWarning("You must be in Play Mode to test API calls.");
        }
    }
}
