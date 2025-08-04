using System;
using System.Collections;
using TMPro;
using UnityEngine;

[Serializable]
public struct UserReg
{
    public string Username;
    public string Password;
}

public class RegisterScript : AuthFormBase
{
    [Header("Register Specific UI")]
    public GameObject registerModal;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    // OnEnable in base class handles clearing the error.
    // We can override it to add more specific logic.
    protected override void OnEnable()
    {
        base.OnEnable(); // Call base method
        usernameInput.text = "Dak";
        passwordInput.text = "password123";
        confirmPasswordInput.text = "password123";
    }

    public bool isJson(string response)
    {
        try
        {
            JsonUtility.FromJson<object>(response);
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
        ClearError(); // Use method from base class

        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            ShowError("Username and password cannot be empty.");
            return;
        }

        if (passwordInput.text != confirmPasswordInput.text)
        {
            ShowError("Passwords do not match.");
            return;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("AuthManager.Instance is null — not initialized yet");
            return;
        }

        string username = usernameInput.text;
        string password = passwordInput.text;

        // The lambda here wraps the AuthManager call for the base class coroutine.
        System.Action<System.Action<bool, string>> registerOperation = (callback) =>
        {
            AuthManager.Instance.Register(username, password, callback);
        };

        StartCoroutine(HandleAuthOperationWithAnimation("Registering", 0.5f, registerOperation));
    }

    protected override void OnAuthSuccess(string message)
    {
        ShowError(message, Color.green);
        Debug.Log("✅ Registration successful!");
        if (registerModal != null)
            registerModal.SetActive(false);

        RefreshAuthUIVisibility();
    }

    protected override void OnAuthFailure(string message)
    {
        ShowError(message, Color.red);
        Debug.LogWarning($"❌ Registration failed: {message}");
    }
}
