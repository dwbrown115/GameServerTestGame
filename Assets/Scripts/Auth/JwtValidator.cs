using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class JwtValidator : MonoBehaviour
{
    public PlayerApiClient playerApiClient;
    public string nextSceneName = "User"; // Scene to load if token is valid

    [SerializeField]
    private string authEndpoint = "http://localhost:5140/authentication";

    [SerializeField]
    private string validateRoute = "/validate";

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
        }
    }

    private IEnumerator ValidateAndFetchData()
    {
        var payload = new TokenValidationRequest
        {
            Token = JwtManager.Instance.GetJwt(),
            RefreshToken = JwtManager.Instance.GetRefreshToken(),
            DeviceId = DeviceUtils.GetDeviceId(),
            UserId = PlayerManager.Instance.GetUserId(),
        };

        Debug.Log($"Payload: {JsonUtility.ToJson(payload)}");
        Debug.Log($"Validation URL: {authEndpoint + validateRoute}");

        string json = JsonUtility.ToJson(payload);
        UnityWebRequest request = new UnityWebRequest(
            authEndpoint + validateRoute,
            UnityWebRequest.kHttpVerbPOST
        );
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        Debug.Log($"🛰️ Server response code: {request.responseCode}");
        Debug.Log($"📨 Raw response: {request.downloadHandler.text}");

#if UNITY_2023_1_OR_NEWER
        bool hasError = request.result != UnityWebRequest.Result.Success;
#else
        bool hasError = request.isNetworkError || request.isHttpError;
#endif

        if (hasError)
        {
            Debug.LogWarning($"❌ Token validation failed: {request.error}");
            JwtManager.Instance.ClearToken();
            yield break; // Stop the coroutine
        }

        // Token validation successful
        var response = JsonUtility.FromJson<LoginResult>(request.downloadHandler.text);
        JwtManager.Instance.SetToken(response);
        Debug.Log("✅ Token validated or refreshed.");

        // Now, fetch player data
        Debug.Log("🔄 Fetching player data...");
        bool playerDataSuccess = false;
        yield return playerApiClient.GetPlayerData(success => playerDataSuccess = success);

        if (playerDataSuccess)
        {
            Debug.Log("✅ Player data fetched successfully. Navigating to next scene.");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError(
                "❌ Failed to fetch player data after successful token validation. Staying on login screen."
            );
            JwtManager.Instance.ClearToken();
        }
    }
}
