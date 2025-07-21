using System;
using TMPro;
using UnityEngine;

[Serializable]
public struct UserReg
{
    public string Username;
    public string Password;
}

public class RegisterScript : MonoBehaviour
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

    public bool isJson(string response)
    {
        try
        {
            JsonUtility.FromJson<object>(response);
            // Debug.Log("Response is valid JSON");
            return true;
        }
        catch (Exception)
        {
            // Debug.LogError("Response is not valid JSON");
            return false;
        }
    }

    public void Register()
    {
        errorText.enabled = false;
        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            errorText.enabled = true;
            errorText.text = "Username and password cannot be empty.";
            return;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("AuthManager.Instance is null — not initialized yet");
            return;
        }

        AuthManager.Instance.Register(
            usernameInput.text,
            passwordInput.text,
            (success, message) =>
            {
                if (success)
                {
                    Debug.Log("✅ Registration successful!");
                    authNavigation.NavigateToUser();
                }
                else
                    Debug.LogWarning($"❌ Registration failed: {message}");
            }
        );
    }
}
