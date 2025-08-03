using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserScreen : MonoBehaviour
{
    // The JwtValidator's job is done before this scene loads.
    // We only need the PlayerApiClient to refresh data if necessary.
    public PlayerApiClient playerApiClient;

    // public AuthNavigation authNavigation;

    public TMP_Text userNameText;

    // public TMP_Text userIdText;

    private void Start()
    {
        // When this screen starts, we assume the token has already been
        // validated by JwtValidator on the previous scene. We just check
        // it again as a safeguard.
        if (JwtManager.Instance.IsTokenValid())
        {
            Debug.Log("✅ User screen initialized with valid token.");
            // Display local data immediately, then start a refresh.
            UpdateUI();
            StartCoroutine(RefreshPlayerData());
        }
        else
        {
            userNameText.text = "";
            // Debug.LogWarning("❌ User screen initialized with invalid token. Navigating home.");
            // if (authNavigation != null)
            //     authNavigation.NavigateToHome();
            // else
            //     SceneManager.LoadScene("Home");
        }
    }

    public void OnLoggedIn()
    {
        // This method can be called from other scripts to refresh the UI
        // after a successful login or registration.
        StartCoroutine(RefreshPlayerData());
        // UpdateUI();
    }

    private IEnumerator RefreshPlayerData()
    {
        bool fetchComplete = false;
        playerApiClient.GetPlayerData(
            onSuccess: (playerData) =>
            {
                // Perform local logic: update PlayerManager and then the UI
                PlayerManager.Instance.SetPlayerData(playerData.userId, playerData.userName);
                UpdateUI();
                fetchComplete = true;
            },
            onError: (error) =>
            {
                Debug.LogError($"Failed to refresh player data: {error}");
                fetchComplete = true;
            }
        );

        yield return new WaitUntil(() => fetchComplete);
    }

    private void UpdateUI()
    {
        userNameText.text = "Welcome back, " + PlayerManager.Instance.GetPlayerName();
        // userIdText.text = PlayerManager.Instance.GetUserId();
    }
}
