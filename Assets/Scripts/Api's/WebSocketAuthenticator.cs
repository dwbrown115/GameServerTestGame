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
        StartCoroutine(AuthenticateCoroutine(authRequest, onComplete));
    }

    private IEnumerator AuthenticateCoroutine(
        WebSocketAuthRequest authRequest,
        Action<WebSocketAuthResponse> onComplete
    )
    {
        string jsonPayload = JsonConvert.SerializeObject(authRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(AuthEndpoint, "POST"))
        {
            // Use the CustomCertificateHandler that we know works.
            request.certificateHandler = new CustomCertificateHandler();
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log("Sending WebSocket authentication request via HTTPS...");
            yield return request.SendWebRequest();

            WebSocketAuthResponse response;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Authentication Error: {request.error}");
                try
                {
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
                    response = new WebSocketAuthResponse
                    {
                        Authenticated = false,
                        Reason = request.error,
                    };
                }
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                response = JsonConvert.DeserializeObject<WebSocketAuthResponse>(responseJson);
                Debug.Log($"HTTPS Auth Response: {JsonConvert.SerializeObject(response)}");
            }

            onComplete?.Invoke(response);
        }
    }
}
