using System;
using TMPro;
using UnityEngine;

public class ChangeUserInfoScreen : MonoBehaviour
{
    public UserScreenNavigation userScreenNavigation;
    public AuthManager authManager;
    public PlayerManager playerManager;

    public TMP_Text errorText;
    public TMP_InputField usernameInput;
    public TMP_InputField oldPasswordInput;
    public TMP_InputField newPasswordInput;

    private void Start()
    {
        errorText.enabled = false;
        string currentName = playerManager.GetPlayerName();
        if (!string.IsNullOrEmpty(currentName))
        {
            usernameInput.text = currentName;
        }
    }

    public void SubmitChanges()
    {
        errorText.enabled = false;

        string newUsername = usernameInput.text;
        string oldPassword = oldPasswordInput.text;
        string newPassword = newPasswordInput.text;

        // Validate: if one password field is filled, both must be.
        if (!string.IsNullOrEmpty(oldPassword) && string.IsNullOrEmpty(newPassword) ||
            string.IsNullOrEmpty(oldPassword) && !string.IsNullOrEmpty(newPassword))
        {
            errorText.text = "Both password fields must be filled to change password.";
            errorText.enabled = true;
            return;
        }

        // Don't submit username if it hasn't changed.
        if (newUsername == playerManager.GetPlayerName())
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

        // authManager.UpdatePlayerInfo(newUsername, oldPassword, newPassword, (success, message) =>
        // {
        //     errorText.text = message;
        //     errorText.enabled = true;
        //     errorText.color = success ? Color.green : Color.red;
        // });
    }
}