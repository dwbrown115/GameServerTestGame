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
        Debug.Log($"WebSocketAuthenticator: Creating payload for authentication request. UserID: {authRequest.UserId}");
        string jsonPayload = JsonConvert.SerializeObject(authRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(AuthEndpoint, "POST"))
        {
            // Use the CustomCertificateHandler that we know works.
            request.certificateHandler = CustomCertificateHandler.Instance; // Ensure using singleton
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"WebSocketAuthenticator: Sending authentication request to {AuthEndpoint}...");
            yield return request.SendWebRequest();
            Debug.Log("WebSocketAuthenticator: Authentication request sent. Processing response.");

            WebSocketAuthResponse response;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"WebSocketAuthenticator: Authentication Error: {request.error}");
                try
                {
                    response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(
                        request.downloadHandler.text
                    );
                    if (string.IsNullOrEmpty(response.Reason))
                    {
                        response.Reason = request.error;
                    }
                    Debug.LogWarning($"WebSocketAuthenticator: Parsed error response: {JsonConvert.SerializeObject(response)}");
                }
                catch (Exception ex)
                {
                    response = new WebSocketAuthResponse
                    {
                        Authenticated = false,
                        Reason = request.error,
                    };
                    Debug.LogError($"WebSocketAuthenticator: Failed to parse error response: {ex.Message}");
                }
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(responseJson);
                Debug.Log($"WebSocketAuthenticator: HTTPS Auth Response: {JsonConvert.SerializeObject(response)}");
            }

            onComplete?.Invoke(response);
            Debug.Log("WebSocketAuthenticator: Authentication process completed.");
        }
    }
}