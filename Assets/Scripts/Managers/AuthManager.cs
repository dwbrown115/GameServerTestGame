using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AuthenticationRequest
{
    public string Username;
    public string Password;
    public string DeviceId;
}

[Serializable]
public class LogoutRequest
{
    public string UserID;
    public string RefreshToken;
    public string DeviceId;
}

[Serializable]
public class PasswordChangePayload
{
    public string OldPassword;
    public string NewPassword;
}

[Serializable]
public struct UpdateResponse
{
    public bool success;
    public string message;
}

[Serializable]
public class PlayerChanges
{
    [JsonProperty("Username", NullValueHandling = NullValueHandling.Ignore)]
    public string Username { get; set; }

    [JsonProperty("Password", NullValueHandling = NullValueHandling.Ignore)]
    public PasswordChangePayload Password { get; set; }
}

[Serializable]
public class UpdatePlayerRequest
{
    public string UserId { get; set; }
    public string DeviceId { get; set; }
    public string RefreshToken { get; set; }
    public PlayerChanges Changes { get; set; }
}

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;

    [SerializeField]
    private string endpoint = "http://localhost:5140";

    [SerializeField]
    private string authEndpointPrefix = "/authentication";

    [SerializeField]
    private string playerEndpointPrefix = "/player";

    [SerializeField]
    private string registerRoute = "/register";

    [SerializeField]
    private string loginRoute = "/login";

    [SerializeField]
    private string logoutRoute = "/logout";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void Register(string username, string password, Action<bool, string> callback)
    {
        var payload = new AuthenticationRequest
        {
            Username = username,
            Password = password,
            DeviceId = DeviceUtils.GetDeviceId(),
        };
        string json = JsonConvert.SerializeObject(payload);
        UnityWebRequest request = new UnityWebRequest(
            endpoint + authEndpointPrefix + registerRoute,
            "POST"
        );
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        StartCoroutine(SendRequest(request, callback));
    }

    public void Login(string username, string password, Action<bool, string> callback)
    {
        var payload = new AuthenticationRequest
        {
            Username = username,
            Password = password,
            DeviceId = DeviceUtils.GetDeviceId(),
        };

        string json = JsonConvert.SerializeObject(payload);

        UnityWebRequest request = new UnityWebRequest(
            endpoint + authEndpointPrefix + loginRoute,
            "POST"
        );
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        StartCoroutine(
            SendRequest(
                request,
                (success, response) =>
                {
                    if (success)
                    {
                        Debug.Log($"🌐 Login raw response: {response}");

                        LoginResult loginResult = null;
                        try
                        {
                            loginResult = JsonConvert.DeserializeObject<LoginResult>(response);
                            Debug.Log(
                                $"🌐 Parsed LoginResult: {JsonConvert.SerializeObject(loginResult)}"
                            );
                            if (!string.IsNullOrEmpty(loginResult?.token))
                            {
                                Debug.Log($"✅ JWT: {loginResult.token}");
                                Debug.Log($"🔁 Refresh Token: {loginResult.refreshToken}");
                                Debug.Log($"⏳ Expires At: {loginResult.expiresAt}");
                                JwtManager.Instance.SetToken(loginResult);
                            }
                            else
                            {
                                Debug.LogWarning("⚠️ LoginResult is empty or failed to parse.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"❌ Failed to parse login result: {ex.Message}");
                        }
                    }

                    callback?.Invoke(success, response);
                }
            )
        );
    }

    public void UpdatePlayerInfo(
        string newUsername,
        string oldPassword,
        string newPassword,
        Action<bool, string> callback
    )
    {
        var changes = new PlayerChanges();
        bool hasChanges = false;

        if (!string.IsNullOrEmpty(newUsername))
        {
            changes.Username = newUsername;
            hasChanges = true;
        }

        if (!string.IsNullOrEmpty(oldPassword) && !string.IsNullOrEmpty(newPassword))
        {
            changes.Password = new PasswordChangePayload
            {
                OldPassword = oldPassword,
                NewPassword = newPassword,
            };
            hasChanges = true;
        }

        if (!hasChanges)
        {
            callback?.Invoke(false, "No changes were provided to update.");
            return;
        }

        var payload = new UpdatePlayerRequest
        {
            UserId = PlayerManager.Instance.GetUserId(),
            DeviceId = DeviceUtils.GetDeviceId(),
            RefreshToken = JwtManager.Instance.GetRefreshToken(),
            Changes = changes,
        };

        string payloadJson = JsonConvert.SerializeObject(payload);

        UnityWebRequest request = new UnityWebRequest(
            endpoint + playerEndpointPrefix + "/update",
            "PATCH"
        );
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        string token = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }
        StartCoroutine(
            SendRequest(
                request,
                (success, response) =>
                {
                    if (!success)
                    {
                        callback?.Invoke(false, response);
                        return;
                    }

                    try
                    {
                        var updateResponse = JsonConvert.DeserializeObject<UpdateResponse>(
                            response
                        );
                        Debug.Log(
                            $"Update response: {JsonConvert.SerializeObject(updateResponse)}"
                        );

                        if (updateResponse.success)
                        {
                            // If the username was successfully changed, update it in the PlayerManager
                            if (!string.IsNullOrEmpty(newUsername))
                            {
                                PlayerManager.Instance.SetPlayerData(
                                    PlayerManager.Instance.GetUserId(),
                                    newUsername
                                );
                            }
                            callback?.Invoke(true, updateResponse.message);
                        }
                        else
                        {
                            callback?.Invoke(false, updateResponse.message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to parse update response: {ex.Message}");
                        callback?.Invoke(false, "Failed to parse server response.");
                    }
                }
            )
        );
    }

    public void Logout(Action<bool, string> callback)
    {
        Debug.Log("Logging out");
        var payload = new LogoutRequest
        {
            RefreshToken = JwtManager.Instance.GetRefreshToken(),
            DeviceId = DeviceUtils.GetDeviceId(),
        };
        Debug.Log($"Payload: {JsonConvert.SerializeObject(payload)}");

        string json = JsonConvert.SerializeObject(payload);

        UnityWebRequest request = new UnityWebRequest(
            endpoint + authEndpointPrefix + logoutRoute,
            "POST"
        );
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        string token = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }
        StartCoroutine(
            SendRequest(
                request,
                (success, response) =>
                {
                    Debug.Log($"Logout response: {response}");
                    Debug.Log($"Logout success: {success}");
                    if (success)
                    {
                        JwtManager.Instance.ClearToken();
                        Debug.Log("✅ Logout successful. Player data cleared.");
                    }
                    else
                    {
                        Debug.LogWarning($"❌ Logout failed: {response}");
                    }

                    callback?.Invoke(success, response);
                }
            )
        );
    }

    public void ValidateToken(Action<bool, LoginResult> callback)
    {
        var payload = new TokenValidationRequest
        {
            Token = JwtManager.Instance.GetJwt(),
            RefreshToken = JwtManager.Instance.GetRefreshToken(),
            DeviceId = DeviceUtils.GetDeviceId(),
            UserId = PlayerManager.Instance.GetUserId(),
        };

        string json = JsonConvert.SerializeObject(payload);
        string url = endpoint + authEndpointPrefix + "/validate";
        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // Validation requires an authorization token
        string token = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }

        StartCoroutine(
            SendRequest(
                request,
                (success, response) =>
                {
                    if (success)
                    {
                        var loginResult = JsonConvert.DeserializeObject<LoginResult>(response);
                        callback?.Invoke(true, loginResult);
                    }
                    else
                    {
                        callback?.Invoke(false, null);
                    }
                }
            )
        );
    }

    private IEnumerator SendRequest(UnityWebRequest request, Action<bool, string> callback)
    {
        yield return request.SendWebRequest();

#if UNITY_2023_1_OR_NEWER
        bool hasError = request.result != UnityWebRequest.Result.Success;
        Debug.Log($"🛰️ Server response code: {hasError}");
#else
        bool hasError = request.isNetworkError || request.isHttpError;
        Debug.Log($"🛰️ Server response code: {hasError}");
#endif
        // Log the raw response text for debugging.
        Debug.Log($"🛰️ Server response code: {request.responseCode}");

        string responseText = request.downloadHandler?.text ?? "";

        Debug.Log($"📨 Raw response: {responseText}");

        if (hasError)
        {
            Debug.LogWarning($"❌ Request failed: {request.error} — Response: {responseText}");
            if (request.responseCode == 401)
            {
                Debug.LogWarning(
                    "Unauthorized (401). Token may be expired or invalid. Clearing token."
                );
                JwtManager.Instance.ClearToken();
            }
            callback?.Invoke(false, responseText);
        }
        else
        {
            Debug.Log($"✅ Request to {request.url} successful. Response: {responseText}");
            callback?.Invoke(true, responseText);
        }
    }
}
