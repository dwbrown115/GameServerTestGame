using System;
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
    public UserScreenNavigation userScreenNavigation;

    private AuthManager authManager;
    private PlayerManager playerManager;
    public TMP_Text errorText;
    public TMP_InputField usernameInput;
    public TMP_InputField oldPasswordInput;
    public TMP_InputField newPasswordInput;

    // Private fields to hold the initial player data
    private string _currentUserId;
    private string _currentUsername;

    private void Awake()
    {
        // Use singletons to get manager instances.
        // This is more robust than relying on Inspector assignments.
        authManager = AuthManager.Instance;
        playerManager = PlayerManager.Instance;
    }

    private void Start()
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
            _currentUserId = playerData.userId;
            _currentUsername = playerData.userName;
            usernameInput.text = _currentUsername;
        }
        else
        {
            Debug.LogError("Player data is not available.");
            errorText.text = "Could not load player data.";
            errorText.enabled = true;
            DisableForm();
            Invoke(nameof(NavigateBack), 3f); // Navigate back after 3 seconds
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

        // 4. Use the UpdatePlayerInfo function
        authManager.UpdatePlayerInfo(
            newUsername,
            oldPassword,
            newPassword,
            (success, message) =>
            {
                Debug.Log($"UpdatePlayerInfo callback: {success}, {message}");

                string displayMessage = message;
                if (!success)
                {
                    try
                    {
                        // Attempt to parse the error response to get the nested message.
                        ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(message);
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
                errorText.color = success ? Color.green : Color.red;

                if (success)
                {
                    // Optionally navigate back after a short delay
                    // to let the user see the success message.
                    Invoke(nameof(NavigateBack), 2f);
                }
            }
        );
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

    private void NavigateBack()
    {
        userScreenNavigation.NavigateToUserScreen();
    }

    private void DisableForm()
    {
        usernameInput.interactable = false;
        oldPasswordInput.interactable = false;
        newPasswordInput.interactable = false;
    }
}
