using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        string json = JsonUtility.ToJson(payload);
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

        string json = JsonUtility.ToJson(payload);

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
                            loginResult = JsonUtility.FromJson<LoginResult>(response);
                            Debug.Log($"🌐 Parsed LoginResult: {JsonUtility.ToJson(loginResult)}");
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
        // This method manually constructs the JSON payload. This is necessary because
        // the server expects a dynamic 'Changes' object that only includes non-null
        // fields, and Unity's JsonUtility does not support omitting null fields during
        // serialization.
        // This also ensures all keys are correctly in PascalCase.

        var changesJsonParts = new List<string>();

        // Conditionally build the "changes" object to only include what's changed.
        if (!string.IsNullOrEmpty(newUsername))
        {
            // Manually quote the string value to make it a valid JSON string.
            changesJsonParts.Add($"\"Username\": \"{newUsername}\"");
        }

        if (!string.IsNullOrEmpty(oldPassword) && !string.IsNullOrEmpty(newPassword))
        {
            var passwordPayload = new PasswordChangePayload
            {
                OldPassword = oldPassword,
                NewPassword = newPassword,
            };
            // Use JsonUtility for the nested object, as it handles that correctly.
            string passwordJson = JsonUtility.ToJson(passwordPayload);
            changesJsonParts.Add($"\"Password\": {passwordJson}");
        }

        string changesJson = string.Join(",", changesJsonParts.ToArray());

        // Manually construct the payload to ensure correct casing and structure,
        // matching the server's expected schema. This payload is NOT wrapped
        // in a top-level "request" object.
        string payloadJson =
            $"{{ "
            + $"\"UserId\": \"{PlayerManager.Instance.GetUserId()}\", "
            + $"\"DeviceId\": \"{DeviceUtils.GetDeviceId()}\", "
            + $"\"RefreshToken\": \"{JwtManager.Instance.GetRefreshToken()}\", "
            + $"\"Changes\": {{ {changesJson} }} "
            + $"}}";

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
                        var updateResponse = JsonUtility.FromJson<UpdateResponse>(response);
                        Debug.Log($"Update response: {JsonUtility.ToJson(updateResponse)}");

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
        Debug.Log($"Payload: {JsonUtility.ToJson(payload)}");

        string json = JsonUtility.ToJson(payload);

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
