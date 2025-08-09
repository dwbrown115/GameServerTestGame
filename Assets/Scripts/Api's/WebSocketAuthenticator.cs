using System;
using System.Collections;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

// --- Data Models ---
// These classes should match your SharedLibrary definitions.
// If you have a shared DLL for your Unity project, you can reference it and remove these.
[System.Serializable]
public class WebSocketAuthRequest
{
    public string DeviceId;
    public string UserId;
    public string JwtToken;
    public string RefreshToken;
}

[System.Serializable]
public class WebSocketAuthResponse
{
    public bool Authenticated;
    public string SessionId;
    public string Reason;
    public string Token;
    public string RefreshToken;
}

public class WebSocketAuthenticator : MonoBehaviour
{
    // IMPORTANT: Replace with your server's address
    private const string AuthEndpoint = "http://localhost:5140/ws/auth";

    /// <summary>
    /// Starts the authentication process by sending credentials to the server.
    /// </summary>
    /// <param name="authRequest">The request object containing all necessary credentials.</param>
    /// <param name="onComplete">A callback action that will be invoked with the server's response.</param>
    public void Authenticate(
        WebSocketAuthRequest authRequest,
        Action<WebSocketAuthResponse> onComplete
    )
    {
        StartCoroutine(AuthenticateCoroutine(authRequest, onComplete));
    }

    private IEnumerator AuthenticateCoroutine(
        WebSocketAuthRequest authRequest,
        Action<WebSocketAuthResponse> onComplete
    )
    {
        // 1. Create the request payload
        string jsonPayload = JsonConvert.SerializeObject(authRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        // 2. Create and configure the UnityWebRequest
        using (UnityWebRequest request = new UnityWebRequest(AuthEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 3. Send the request and wait for the response
            Debug.Log("Sending WebSocket authentication request...");
            yield return request.SendWebRequest();

            // 4. Handle the response
            WebSocketAuthResponse response;
            // Debug.Log($"Response Code: {response}");
            if (
                request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
            )
            {
                Debug.LogError($"Authentication Error: {request.error}");
                try
                {
                    // Try to parse the error response from the server body
                    response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(
                        request.downloadHandler.text
                    );
                    if (string.IsNullOrEmpty(response.Reason))
                    {
                        response.Reason = request.error;
                    }
                }
                catch
                {
                    // If the body isn't a valid JSON, create a new response object
                    response = new WebSocketAuthResponse
                    {
                        Authenticated = false,
                        Reason = request.error,
                    };
                }
            }
            else
            {
                // Successfully received a response from the server
                string responseJson = request.downloadHandler.text;
                response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(responseJson);
                Debug.Log($"Response Code: {JsonConvert.SerializeObject(response)}");
            }

            // 5. Invoke the callback with the final response object
            onComplete?.Invoke(response);
        }
    }
}
