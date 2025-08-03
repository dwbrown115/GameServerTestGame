using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeScreenScript : MonoBehaviour
{
    /// <summary>
    /// A generic method to show a specific panel. You can hook this up to any button's
    /// OnClick event in the Inspector and drag the panel you want to show into the field.
    /// </summary>
    /// <param name="panelToShow">The panel GameObject to activate.</param>
    public void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow != null)
        {
            Debug.Log($"Showing panel: {panelToShow.name}");
            panelToShow.SetActive(true);
        }
    }

    public void HidePanel(GameObject panelToHide)
    {
        if (panelToHide != null)
        {
            Debug.Log($"Hiding panel: {panelToHide.name}");
            panelToHide.SetActive(false);
        }
    }

    // The individual button click handlers for showing panels are no longer needed.
    // You can connect your Login, Register, and User buttons directly to the
    // ShowPanel(GameObject) method in the Inspector.

    public void OnExitButtonClicked()
    {
        Debug.Log("Exit button clicked. Exiting the application.");
        // Exit the application
#if !UNITY_EDITOR
        Application.Quit();
#endif
    }

    public void OnSettingsButtonClicked()
    {
        Debug.Log("Settings button clicked. Navigating to settings screen.");
        // Navigate to the settings screen
        // Example: SceneManager.LoadScene("SettingsScreen");
        SceneManager.LoadScene("Settings");
    }

    public void OnCustomizationButtonClicked()
    {
        Debug.Log("Customization button clicked. Navigating to customization screen.");
        // Navigate to the customization screen
        // Example: SceneManager.LoadScene("CustomizationScreen");
        SceneManager.LoadScene("Customization");
    }

    public void OnPlayButtonClicked()
    {
        Debug.Log("Play button clicked. Starting the game.");
        // Start the game
        // This could be a method in your game manager or a direct scene load
        // Example: SceneManager.LoadScene("GameScene");
        SceneManager.LoadScene("WebsocketTestGame");
    }

    public void OnLeaderboardButtonClicked()
    {
        Debug.Log("Leaderboard button clicked. Navigating to leaderboard screen.");
        // Navigate to the leaderboard screen
        SceneManager.LoadScene("Leaderboard");
    }
}
