using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LoginScript : MonoBehaviour
{
    // public AuthNavigation authNavigation;
    // public HomeScreenScript homeScreenScript;
    public AuthVisibilityHandler loginVisibilityHandler;
    public AuthVisibilityHandler userVisibilityHandler;
    public AuthVisibilityHandler userNameVisibilityHandler;
    public AuthVisibilityHandler logOutVisibilityHandler;
    public UserScreen userScreen;
    public GameObject loginModal;
    public GameObject loadingModal;
    public GameObject UsernameTextObject;
    public LoadingTextAnimator loadingAnimator;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text errorText;

    private void OnEnable()
    {
        usernameInput.text = "Dak";
        passwordInput.text = "password123";
        errorText.text = "";
        errorText.enabled = false;
    }

    public void Login()
    {
        LoginUser(usernameInput.text, passwordInput.text);
    }

    public void LoginUser(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            errorText.enabled = true;
            errorText.text = "Username and password cannot be empty.";
            return;
        }

        // We wrap the login logic in a coroutine to ensure the loading animation
        // is visible for a minimum duration, even if the server responds instantly.
        StartCoroutine(LoginCoroutine(username, password));
    }

    private IEnumerator LoginCoroutine(string username, string password)
    {
        float minAnimationTime = 1f;
        float startTime = Time.time;

        // Show the modal and start the animation
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Logging In");

        bool requestDone = false;
        bool successResult = false;
        string responseResult = "";

        AuthManager.Instance.Login(
            username,
            password,
            (success, response) =>
            {
                successResult = success;
                responseResult = response;
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

        // Stop the animation.
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();

        if (successResult)
        {
            // On success, we immediately hide the modals and transition to the user screen.
            // The UserScreen's OnLoggedIn() can handle displaying a welcome message.
            if (loadingModal != null)
                loadingModal.SetActive(false);
            loginModal.SetActive(false);

            Debug.Log("✅ Login successful!");
            userScreen.OnLoggedIn(); // Notify user screen to refresh data
            loginVisibilityHandler.RefreshVisibility();
            userVisibilityHandler.RefreshVisibility();
            logOutVisibilityHandler.RefreshVisibility();
            UsernameTextObject.SetActive(true);
        }
        else
        {
            // On failure, keep the modal visible to show the error message.
            if (loadingModal != null)
                loadingModal.SetActive(false);
            errorText.enabled = true;
            errorText.text = "Username or password is incorrect.";
            errorText.color = Color.red;
            Debug.LogWarning($"❌ Login failed: {responseResult}");
        }
    }
}
