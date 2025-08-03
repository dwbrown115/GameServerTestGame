using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JwtValidator : MonoBehaviour
{
    public PlayerApiClient playerApiClient;
    public string nextSceneName = "User"; // Scene to load if token is valid

    private void Start()
    {
        if (JwtManager.Instance.IsTokenValid())
        {
            Debug.Log("✅ Token is valid locally. Validating with server...");
            StartCoroutine(ValidateAndFetchData());
        }
        else
        {
            Debug.LogWarning("❌ No valid local token. Staying on login screen.");
            JwtManager.Instance.ClearToken();
            SceneManager.LoadScene("Home");
        }
    }

    private IEnumerator ValidateAndFetchData()
    {
        bool validationComplete = false;
        bool validationSuccess = false;
        LoginResult loginResult = null;

        // 1. Call the centralized validation method in AuthManager
        AuthManager.Instance.ValidateToken(
            (success, result) =>
            {
                validationSuccess = success;
                loginResult = result;
                validationComplete = true;
            }
        );

        // Wait for the validation web request to complete
        yield return new WaitUntil(() => validationComplete);

        if (!validationSuccess)
        {
            Debug.LogWarning("❌ Token validation failed. Navigating home.");
            JwtManager.Instance.ClearToken();
            SceneManager.LoadScene("Home");
            yield break; // Stop the coroutine
        }

        // 2. Perform local logic: Update the JWT Manager with the new token info
        JwtManager.Instance.SetToken(loginResult);
        Debug.Log("✅ Token validated or refreshed.");

        // 3. Call the PlayerApiClient to fetch data
        Debug.Log("🔄 Fetching player data...");
        bool fetchComplete = false;
        bool fetchSuccess = false;
        PlayerResponse playerResponse = null;

        playerApiClient.GetPlayerData(
            onSuccess: (playerData) =>
            {
                fetchSuccess = true;
                playerResponse = playerData;
                fetchComplete = true;
            },
            onError: (error) =>
            {
                fetchSuccess = false;
                fetchComplete = true;
            }
        );

        yield return new WaitUntil(() => fetchComplete);

        if (fetchSuccess)
        {
            // 4. Perform local logic: Update the PlayerManager with the fetched data
            PlayerManager.Instance.SetPlayerData(playerResponse.userId, playerResponse.userName);
            Debug.Log("✅ Player data fetched successfully. Navigating to next scene.");
            SceneManager.LoadScene("Home");
        }
        else
        {
            Debug.LogError(
                "❌ Failed to fetch player data after successful token validation. Staying on login screen."
            );
            JwtManager.Instance.ClearToken();
            SceneManager.LoadScene("Home");
        }
    }
}
