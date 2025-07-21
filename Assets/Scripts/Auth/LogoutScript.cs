using System;
using System.Text;
using UnityEngine;

public class LogoutScript : MonoBehaviour
{
    public AuthNavigation authNavigation;

    public void Logout()
    {
        // Debug.Log("Logout button clicked");
        AuthManager.Instance.Logout(
            (success, message) =>
            {
                if (success)
                {
                    // Navigate to login screen or splash
                    // authNavigation.ShowLoginUI();
                    // Debug.Log("User logged out successfully");
                    // PlayerManager.Instance.ClearPlayerData(); // This is now handled by AuthManager
                    Debug.Log("User logged out successfully");
                    authNavigation.NavigateToHome();
                }
                else
                {
                    Debug.LogWarning("Logout failed: " + message);
                }
            }
        );
    }
}
