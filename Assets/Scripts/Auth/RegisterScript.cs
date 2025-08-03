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

public class RegisterScript : MonoBehaviour
{
    // public AuthNavigation authNavigation;
    public AuthVisibilityHandler loginVisibilityHandler;
    public AuthVisibilityHandler userVisibilityHandler;
    public AuthVisibilityHandler userNameVisibilityHandler;
    public AuthVisibilityHandler logOutVisibilityHandler;
    public GameObject loadingModal; // Keep this to show/hide the panel
    public LoadingTextAnimator loadingAnimator; // Add this to control the animation
    public GameObject registerModal;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;
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

    public void ClearError()
    {
        errorText.enabled = false;
        errorText.text = string.Empty;
    }

    public void ShowError(string message)
    {
        errorText.enabled = true;
        errorText.text = message;
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

        if (passwordInput.text != confirmPasswordInput.text)
        {
            errorText.enabled = true;
            errorText.text = "Passwords do not match.";
            return;
        }

        if (AuthManager.Instance == null)
        {
            Debug.LogError("AuthManager.Instance is null — not initialized yet");
            return;
        }

        // We wrap the registration logic in a coroutine to ensure the loading animation
        // is visible for a minimum duration, even if the server responds instantly.
        StartCoroutine(RegisterWithAnimation());
    }

    private IEnumerator RegisterWithAnimation()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;
        float minAnimationTime = 0.5f;
        float startTime = Time.time;

        // Show the modal and start the animation
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Registering");

        bool requestDone = false;
        bool successResult = false;
        string responseMessage = "";

        AuthManager.Instance.Register(
            username,
            password,
            (success, message) =>
            {
                successResult = success;
                responseMessage = message;
                requestDone = true;
            }
        );

        // Wait for the web request to complete
        yield return new WaitUntil(() => requestDone);

        // Ensure the animation plays for a minimum amount of time
        float elapsedTime = Time.time - startTime;
        if (elapsedTime < minAnimationTime)
        {
            yield return new WaitForSeconds(minAnimationTime - elapsedTime);
        }

        // Stop the animation and hide the modal
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        if (successResult)
        {
            errorText.enabled = true;
            errorText.text = responseMessage;
            errorText.color = Color.green;
            Debug.Log("✅ Registration successful!");
            // authNavigation.NavigateToUser();
            registerModal.SetActive(false);
            userVisibilityHandler.RefreshVisibility();
            loginVisibilityHandler.RefreshVisibility();
            logOutVisibilityHandler.RefreshVisibility();
        }
        else
        {
            errorText.enabled = true;
            errorText.text = responseMessage;
            errorText.color = Color.red;
            Debug.LogWarning($"❌ Registration failed: {responseMessage}");
        }
    }
}
