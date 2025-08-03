using System;
using System.Text;
using TMPro;
using UnityEngine;

public class LogoutScript : MonoBehaviour
{
    // public AuthNavigation authNavigation;
    public AuthVisibilityHandler loginVisibilityHandler;
    public AuthVisibilityHandler userVisibilityHandler;
    public AuthVisibilityHandler userNameVisibilityHandler;
    public AuthVisibilityHandler logOutVisibilityHandler;
    public GameObject loadingModal;
    public TMP_Text usernameText;

    public void Logout()
    {
        // Show the loading modal while waiting for the server response.
        // if (loadingModal != null)
        //     loadingModal.SetActive(true);

        // Debug.Log("Logout button clicked");
        AuthManager.Instance.Logout(
            (success, message) =>
            {
                // Hide the loading modal now that we have a response.
                // if (loadingModal != null)
                //     loadingModal.SetActive(false);
                if (success)
                {
                    // Navigate to login screen or splash
                    // authNavigation.ShowLoginUI();
                    // Debug.Log("User logged out successfully");
                    // PlayerManager.Instance.ClearPlayerData(); // This is now handled by AuthManager
                    Debug.Log("User logged out successfully");
                    // authNavigation.NavigateToHome();
                    loginVisibilityHandler.RefreshVisibility();
                    userVisibilityHandler.RefreshVisibility();
                    logOutVisibilityHandler.RefreshVisibility();
                    usernameText.text = "";
                }
                else
                {
                    Debug.LogWarning("Logout failed: " + message);
                }
            }
        );
    }
}
