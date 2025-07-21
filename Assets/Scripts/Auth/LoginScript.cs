using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LoginScript : MonoBehaviour
{
    public AuthNavigation authNavigation;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text errorText;

    private void Start()
    {
        usernameInput.text = "Dak";
        passwordInput.text = "password123";
        errorText.enabled = false;
    }

    public void Login()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            errorText.enabled = true;
            errorText.text = "Username and password cannot be empty.";
            return;
        }

        AuthManager.Instance.Login(
            username,
            password,
            (success, response) =>
            {
                if (success)
                {
                    Debug.Log("✅ Login successful!");
                    Debug.Log($"JWT: {JwtManager.Instance.GetJwt()}");
                    Debug.Log($"Refresh Token: {JwtManager.Instance.GetRefreshToken()}");
                    // Debug.Log($"Token Expires At: {JwtManager.Instance.GetExpiresAt()}");
                    Debug.Log($"User ID: {PlayerManager.Instance.GetUserId()}");
                    authNavigation.NavigateToUser();
                }
                else
                    Debug.LogWarning($"❌ Login failed: {response}");
            }
        );
    }
}
