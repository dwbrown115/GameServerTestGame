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
    public LoadingTextAnimator loadingAnimator;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public TMP_Text errorText;

    private void Start()
    {
        usernameInput.text = "Jin";
        passwordInput.text = "password123";
        errorText.enabled = false;
    }

    public void Login()
    {
        if (string.IsNullOrEmpty(usernameInput.text) || string.IsNullOrEmpty(passwordInput.text))
        {
            errorText.enabled = true;
            errorText.text = "Username and password cannot be empty.";
            return;
        }

        // We wrap the login logic in a coroutine to ensure the loading animation
        // is visible for a minimum duration, even if the server responds instantly.
        StartCoroutine(LoginWithAnimation());
    }

    private IEnumerator LoginWithAnimation()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;
        float minAnimationTime = 0.5f;
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

        // Stop the animation and hide the modal
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        if (successResult)
        {
            errorText.enabled = true;
            errorText.text = "Login successful!";
            errorText.color = Color.green;
            Debug.Log("✅ Login successful!");
            loginModal.SetActive(false);
            userScreen.OnLoggedIn(); // Notify user screen to refresh data
            loginVisibilityHandler.RefreshVisibility();
            userVisibilityHandler.RefreshVisibility();
            logOutVisibilityHandler.RefreshVisibility();
        }
        else
        {
            Debug.LogWarning($"❌ Login failed: {responseResult}");
            errorText.enabled = true;
            errorText.text = responseResult;
            errorText.color = Color.red;
        }
    }
}
