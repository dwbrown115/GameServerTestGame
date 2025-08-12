using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LoginResult
{
    public string userId;
    public string token;
    public string refreshToken;
    public string expiresAt;
}

public class LoginManager : MonoBehaviour
{
    private const string serverUrl = "https://localhost:7123/authentication/validate";

    public void AttemptLogin()
    {
        if (!JwtManager.Instance.IsTokenValid())
        {
            Debug.Log("🔐 No valid token found. Prompting user to log in.");
            // Show login UI or call AuthManager.Login()
            return;
        }
        else
        {
            Debug.Log("🛰️ Attempting token validation with server...");
            // AuthManager.Instance.ValidateToken(
            //     (success, message) =>
            //     {
            //         if (success)
            //         {
            //             Debug.Log("🎉 Token validated or refreshed.");
            //             // authNavigation.OnLoginSuccess();
            //         }
            //         else
            //         {
            //             Debug.LogWarning(
            //                 $"⚠️ Token validation failed: {message}. Prompting login."
            //             );
            //             // authNavigation.ShowLoginUI();
            //         }
            //     }
            // );
        }
    }
}
