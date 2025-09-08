using UnityEngine;
using UnityEngine.SceneManagement;

public class AuthNavigation : MonoBehaviour
{
    public void NavigateToHome()
    {
        Debug.Log("Navigating to Home");
        SceneManager.LoadScene("Home", LoadSceneMode.Single);
    }

    public void NavigateToWebsocketTestGame()
    {
        Debug.Log("Navigating to Websocket Test Game");
        // Re-fetch active skin and compare to saved before entering the game
        if (PlayerSkinManager.Instance != null)
        {
            PlayerSkinManager.Instance.ValidateActiveSkinAgainstSaved();
        }
        SceneManager.LoadScene("WebsocketTestGame", LoadSceneMode.Single);
    }

    public void NavigateToSettings()
    {
        Debug.Log("Navigating to Settings");
        SceneManager.LoadScene("Settings", LoadSceneMode.Single);
    }

    public void NavigateToCustomization()
    {
        Debug.Log("Navigating to Customization");
        SceneManager.LoadScene("Customization", LoadSceneMode.Single);
    }

    public void NavigateToLeaderboard()
    {
        Debug.Log("Navigating to Leaderboard");
        SceneManager.LoadScene("Leaderboard", LoadSceneMode.Single);
    }
}
