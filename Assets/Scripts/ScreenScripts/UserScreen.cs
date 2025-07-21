using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UserScreen : MonoBehaviour
{
    // The JwtValidator's job is done before this scene loads.
    // We only need the PlayerApiClient to refresh data if necessary.
    public PlayerApiClient playerApiClient;
    public AuthNavigation authNavigation;

    public TMP_Text userNameText;
    public TMP_Text userIdText;

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
            Debug.LogWarning("❌ User screen initialized with invalid token. Navigating home.");
            if (authNavigation != null)
                authNavigation.NavigateToHome();
            else
                SceneManager.LoadScene("Home");
        }
    }

    private IEnumerator RefreshPlayerData()
    {
        bool success = false;
        // This waits for the API call to complete before continuing.
        yield return playerApiClient.GetPlayerData(s => success = s);

        if (success)
            UpdateUI(); // Refresh UI with new data.
    }

    private void UpdateUI()
    {
        userNameText.text = PlayerManager.Instance.GetPlayerName();
        userIdText.text = PlayerManager.Instance.GetUserId();
    }
}
