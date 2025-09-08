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
    // Ensure JSON uses "UserId"
    [JsonProperty("UserId")]
    public string UserId;
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
    private string endpoint = "https://localhost:7123";

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
        StartCoroutine(
            Net.HttpRequest.Send(
                "POST",
                endpoint + authEndpointPrefix + registerRoute,
                resp =>
                {
                    HandleBasicResponse("Register", resp, callback);
                },
                json,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                },
                CustomCertificateHandler.Instance,
                10
            )
        );
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

        StartCoroutine(
            Net.HttpRequest.Send(
                "POST",
                endpoint + authEndpointPrefix + loginRoute,
                resp =>
                {
                    bool success =
                        resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                    string response = resp.body ?? string.Empty;
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
                                JwtManager.Instance.SetToken(
                                    loginResult.token,
                                    loginResult.refreshToken,
                                    loginResult.userId,
                                    JwtManager.ParseJwtExpiry(loginResult.token)
                                );
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
                },
                json,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                },
                CustomCertificateHandler.Instance,
                10
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

        var headers = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
        };
        string token = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(token))
        {
            headers["Authorization"] = "Bearer " + token;
        }
        StartCoroutine(
            Net.HttpRequest.Send(
                "PATCH",
                endpoint + playerEndpointPrefix + "/update",
                resp =>
                {
                    bool success =
                        resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                    string response = resp.body ?? string.Empty;
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
                },
                payloadJson,
                headers,
                CustomCertificateHandler.Instance,
                10
            )
        );
    }

    public void Logout(Action<bool, string> callback)
    {
        Debug.Log("Logging out");
        var payload = new LogoutRequest
        {
            UserId = PlayerManager.Instance.GetUserId(),
            RefreshToken = JwtManager.Instance.GetRefreshToken(),
            DeviceId = DeviceUtils.GetDeviceId(),
        };
        if (string.IsNullOrEmpty(payload.UserId))
        {
            Debug.LogWarning("⚠️ Logout: No UserId found in PlayerManager. Proceeding without it.");
        }

        // Mask sensitive tokens in logs
        string rt = payload.RefreshToken ?? string.Empty;
        string maskedRt =
            rt.Length > 8 ? $"{rt.Substring(0, 4)}...{rt.Substring(rt.Length - 4)}" : "(empty)";
        Debug.Log(
            $"Payload => UserId={payload.UserId}, DeviceId={payload.DeviceId}, RefreshToken={maskedRt}"
        );

        string json = JsonConvert.SerializeObject(payload);

        var headersLogout = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
        };
        string tokenLogout = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(tokenLogout))
        {
            headersLogout["Authorization"] = "Bearer " + tokenLogout;
        }
        StartCoroutine(
            Net.HttpRequest.Send(
                "POST",
                endpoint + authEndpointPrefix + logoutRoute,
                resp =>
                {
                    bool success =
                        resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                    string response = resp.body ?? string.Empty;
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
                },
                json,
                headersLogout,
                CustomCertificateHandler.Instance,
                10
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
        var headersValidate = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
        };
        string tokenVal = JwtManager.Instance.GetJwt();
        if (!string.IsNullOrEmpty(tokenVal))
        {
            headersValidate["Authorization"] = "Bearer " + tokenVal;
        }
        StartCoroutine(
            Net.HttpRequest.Send(
                UnityWebRequest.kHttpVerbPOST,
                url,
                resp =>
                {
                    bool success =
                        resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                    string response = resp.body ?? string.Empty;
                    if (success)
                    {
                        var loginResult = JsonConvert.DeserializeObject<LoginResult>(response);
                        JwtManager.Instance.SetToken(
                            loginResult.token,
                            loginResult.refreshToken,
                            loginResult.userId,
                            JwtManager.ParseJwtExpiry(loginResult.token)
                        );
                        callback?.Invoke(true, loginResult);
                    }
                    else
                    {
                        if (
                            response.Contains(
                                "No valid refresh token record found or it has expired"
                            )
                        )
                        {
                            Debug.LogWarning(
                                "Server indicated refresh token is invalid or expired. Clearing local tokens."
                            );
                            JwtManager.Instance.ClearToken();
                        }
                        callback?.Invoke(false, null);
                    }
                },
                json,
                headersValidate,
                CustomCertificateHandler.Instance,
                10
            )
        );
    }

    private void HandleBasicResponse(
        string label,
        Net.HttpResponse resp,
        Action<bool, string> callback
    )
    {
        bool hasError = resp.result != UnityWebRequest.Result.Success;
        Debug.Log($"🛰️ {label} result hasError={hasError}");
        Debug.Log($"🛰️ Server response code: {resp.statusCode}");
        string responseText = resp.body ?? string.Empty;
        Debug.Log($"📨 Raw response: {responseText}");
        if (hasError)
        {
            Debug.LogWarning($"❌ Request failed: {resp.error} — Response: {responseText}");
            if (resp.statusCode == 401)
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
            Debug.Log($"✅ Request successful. Response: {responseText}");
            callback?.Invoke(true, responseText);
        }
    }
}
