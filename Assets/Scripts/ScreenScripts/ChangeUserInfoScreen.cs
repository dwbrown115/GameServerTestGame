using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public struct ErrorResponse
{
    public string message;
}

public class ChangeUserInfoScreen : MonoBehaviour
{
    // public UserScreenNavigation userScreenNavigation;
    public UserScreen userScreen;
    public GameObject userModal;
    public GameObject loadingModal;
    public LoadingTextAnimator loadingAnimator;
    private AuthManager authManager;
    private PlayerManager playerManager;
    public TMP_Text errorText;
    public TMP_InputField usernameInput;
    public TMP_InputField oldPasswordInput;
    public TMP_InputField newPasswordInput;
    public TMP_InputField confirmPasswordInput;

    // Private fields to hold the initial player data
    private string _currentUsername;

    private void Awake()
    {
        // Use singletons to get manager instances.
        // This is more robust than relying on Inspector assignments.
        authManager = AuthManager.Instance;
        playerManager = PlayerManager.Instance;
    }

    private void OnEnable()
    {
        errorText.enabled = false;

        if (playerManager == null || authManager == null)
        {
            Debug.LogError("Required managers not found in the scene.");
            errorText.text = "Critical error: Managers not found.";
            errorText.enabled = true;
            DisableForm();
            return;
        }

        // 1. Grab player data on start
        PlayerResponse playerData = playerManager.GetPlayerData();
        if (playerData != null)
        {
            _currentUsername = playerData.userName;
            usernameInput.text = _currentUsername;
        }
        else
        {
            Debug.LogError("Player data is not available.");
            errorText.text = "Could not load player data.";
            errorText.enabled = true;
            DisableForm();
            // Invoke(nameof(NavigateBack), 3f); // Navigate back after 3 seconds
        }

        // 2. Add listeners for placeholder behavior
        usernameInput.onSelect.AddListener(OnUsernameSelect);
        usernameInput.onDeselect.AddListener(OnUsernameDeselect);
    }

    private void OnDestroy()
    {
        // Clean up listeners
        if (usernameInput != null)
        {
            usernameInput.onSelect.RemoveListener(OnUsernameSelect);
            usernameInput.onDeselect.RemoveListener(OnUsernameDeselect);
        }
    }

    public void SubmitChanges()
    {
        errorText.enabled = false;

        string newUsername = usernameInput.text;
        string oldPassword = oldPasswordInput.text;
        string newPassword = newPasswordInput.text;
        string confirmPassword = confirmPasswordInput.text;

        // Validate: if one password field is filled, both must be.
        if (
            !string.IsNullOrEmpty(oldPassword) && string.IsNullOrEmpty(newPassword)
            || string.IsNullOrEmpty(oldPassword) && !string.IsNullOrEmpty(newPassword)
        )
        {
            errorText.text = "Both password fields must be filled to change password.";
            errorText.enabled = true;
            return;
        }

        // Validate: if new password is set, it must match confirm password.
        if (!string.IsNullOrEmpty(newPassword) && newPassword != confirmPassword)
        {
            errorText.text = "Passwords do not match.";
            errorText.enabled = true;
            return;
        }

        // 3. Don't submit username if it hasn't changed.
        if (newUsername == _currentUsername)
        {
            newUsername = null;
        }

        // Check if there's anything to submit.
        if (string.IsNullOrEmpty(newUsername) && string.IsNullOrEmpty(oldPassword))
        {
            errorText.text = "No changes to submit.";
            errorText.enabled = true;
            return;
        }

        // We wrap the logic in a coroutine to ensure the loading animation
        // is visible for a minimum duration, even if the server responds instantly.
        StartCoroutine(SubmitChangesWithAnimation());
    }

    private IEnumerator SubmitChangesWithAnimation()
    {
        string newUsername = usernameInput.text;
        string oldPassword = oldPasswordInput.text;
        string newPassword = newPasswordInput.text;

        // Don't submit username if it hasn't changed.
        if (newUsername == _currentUsername)
        {
            newUsername = null;
        }

        float minAnimationTime = 0.5f;
        float startTime = Time.time;

        // Show the modal and start the animation
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Saving Changes");

        bool requestDone = false;
        bool successResult = false;
        string responseMessage = "";

        authManager.UpdatePlayerInfo(
            newUsername,
            oldPassword,
            newPassword,
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

        string displayMessage = responseMessage;
        if (!successResult)
        {
            try
            {
                // Attempt to parse the error response to get the nested message.
                ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(responseMessage);
                if (!string.IsNullOrEmpty(error.message))
                {
                    displayMessage = error.message;
                }
            }
            catch (Exception)
            { /* Not a JSON error message, display as is. */
            }
        }

        errorText.text = displayMessage;
        errorText.enabled = true;
        errorText.color = successResult ? Color.green : Color.red;

        if (successResult)
        {
            userModal.SetActive(false); // Close the modal on success
            userScreen.OnLoggedIn(); // Notify user screen to refresh data
        }
    }

    private void OnUsernameSelect(string text)
    {
        // When the user clicks on the input field, clear it.
        usernameInput.text = "";
    }

    private void OnUsernameDeselect(string text)
    {
        // When the user clicks off, if it's empty, restore the original username.
        if (string.IsNullOrEmpty(usernameInput.text))
        {
            usernameInput.text = _currentUsername;
        }
    }

    // private void NavigateBack()
    // {
    //     // userScreenNavigation.NavigateToUserScreen();
    // }

    private void DisableForm()
    {
        usernameInput.interactable = false;
        oldPasswordInput.interactable = false;
        newPasswordInput.interactable = false;
    }
}
