using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

// --- Data Models ---
[System.Serializable]
public class WebSocketAuthRequest
{
    [JsonProperty("deviceId")]
    public string DeviceId;

    [JsonProperty("userId")]
    public string UserId;

    [JsonProperty("jwtToken")]
    public string JwtToken;

    [JsonProperty("refreshToken")]
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
    // Changed from wss:// to https://
    private const string AuthEndpoint = "https://localhost:7123/ws/auth";

    public void Authenticate(
        WebSocketAuthRequest authRequest,
        Action<WebSocketAuthResponse> onComplete
    )
    {
        Debug.Log("WebSocketAuthenticator: Authenticate method called. Starting coroutine.");
        StartCoroutine(AuthenticateCoroutine(authRequest, onComplete));
    }

    private IEnumerator AuthenticateCoroutine(
        WebSocketAuthRequest authRequest,
        Action<WebSocketAuthResponse> onComplete
    )
    {
        Debug.Log("WebSocketAuthenticator: AuthenticateCoroutine started.");
        Debug.Log(
            $"WebSocketAuthenticator: Creating payload for authentication request. UserID: {authRequest.UserId}"
        );
        string jsonPayload = JsonConvert.SerializeObject(authRequest);
        Debug.Log(
            $"WebSocketAuthenticator: Sending authentication request to {AuthEndpoint} with payload: {jsonPayload}"
        );

        yield return Net.HttpRequest.Send(
            "POST",
            AuthEndpoint,
            resp =>
            {
                Debug.Log(
                    "WebSocketAuthenticator: Authentication request sent. Processing response."
                );
                WebSocketAuthResponse response;
                if (resp.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebSocketAuthenticator: Authentication Error: {resp.error}");
                    try
                    {
                        response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(resp.body);
                        if (response != null && string.IsNullOrEmpty(response.Reason))
                        {
                            response.Reason = resp.error;
                        }
                        Debug.LogWarning(
                            $"WebSocketAuthenticator: Parsed error response: {JsonConvert.SerializeObject(response)}"
                        );
                    }
                    catch (Exception ex)
                    {
                        response = new WebSocketAuthResponse
                        {
                            Authenticated = false,
                            Reason = resp.error,
                        };
                        Debug.LogError(
                            $"WebSocketAuthenticator: Failed to parse error response: {ex.Message}"
                        );
                    }
                }
                else
                {
                    string responseJson = resp.body;
                    response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(responseJson);
                    Debug.Log(
                        $"WebSocketAuthenticator: HTTPS Auth Response: {JsonConvert.SerializeObject(response)}"
                    );
                }

                onComplete?.Invoke(response);
                Debug.Log("WebSocketAuthenticator: Authentication process completed.");
            },
            jsonPayload,
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
            },
            PlayerApiCertificateHandler.Instance,
            10
        );
    }
}
