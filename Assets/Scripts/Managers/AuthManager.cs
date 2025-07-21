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
public class AuthenticationResponse
{
    public string Token;
    public string RefreshToken;
    public DateTime ExpiresAt;
}

[Serializable]
public class LogoutRequest
{
    public string UserID;
    public string RefreshToken;
    public string DeviceId;
}

[Serializable]
public class PasswordChange
{
    public string oldPassword;
    public string newPassword;
}

[Serializable]
public class UpdatePlayerRequest
{
    public string userId;
    public string deviceId;
    public string refreshToken;
    public PlayerChanges changes;
}

[Serializable]
public class PlayerChanges
{
    public string username;
    public PasswordChange password;
}

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance;

    [SerializeField]
    private string authEndpoint = "http://localhost:5140/authentication";

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

        UnityWebRequest request = new UnityWebRequest(authEndpoint + registerRoute, "POST");
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

        UnityWebRequest request = new UnityWebRequest(authEndpoint + loginRoute, "POST");
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
        var payload = new UpdatePlayerRequest
        {
            userId = PlayerManager.Instance.GetUserId(),
            deviceId = DeviceUtils.GetDeviceId(),
            refreshToken = JwtManager.Instance.GetRefreshToken(),
            changes = new PlayerChanges(),
        };

        if (!string.IsNullOrEmpty(newUsername))
        {
            payload.changes.username = newUsername;
        }

        if (!string.IsNullOrEmpty(oldPassword) && !string.IsNullOrEmpty(newPassword))
        {
            payload.changes.password = new PasswordChange
            {
                oldPassword = oldPassword,
                newPassword = newPassword,
            };
        }

        string payloadJson = JsonUtility.ToJson(payload);
        UnityWebRequest request = new UnityWebRequest(authEndpoint + "/update", "PATCH");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

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
                        // On a successful update, the server should return a new LoginResult
                        // with fresh tokens and the updated user info.
                        var loginResult = JsonUtility.FromJson<LoginResult>(response);
                        if (loginResult != null && !string.IsNullOrEmpty(loginResult.token))
                        {
                            JwtManager.Instance.SetToken(loginResult);
                            callback?.Invoke(true, "Player info updated successfully.");
                        }
                        else
                        {
                            callback?.Invoke(
                                false,
                                "Received invalid update response from server."
                            );
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
        UnityWebRequest request = new UnityWebRequest(authEndpoint + logoutRoute, "POST");
        Debug.Log($"Request: {request.url}");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

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
#else
        bool hasError = request.isNetworkError || request.isHttpError;
#endif

        string responseText = request.downloadHandler?.text ?? "";

        if (hasError)
        {
            Debug.LogWarning($"❌ Request failed: {request.error} — Response: {responseText}");
            callback?.Invoke(false, responseText);
        }
        else
        {
            callback?.Invoke(true, responseText);
        }
    }
}
