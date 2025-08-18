using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WebSocketManager : MonoBehaviour
{
    public static WebSocketManager Instance { get; private set; }
    public static event Action<string> OnSessionIdReady;
    // --- Dependencies ---
    [Header("Dependencies")]
    [Tooltip("The authenticator component used to verify WebSocket connections.")]
    [SerializeField]
    private WebSocketAuthenticator _authenticator;

    [Header("UI Elements")]
    [Tooltip("The modal/panel to show when an error occurs.")]
    public GameObject errorModal;

    [Tooltip("The TextMeshPro component to display the error message in.")]
    public TMP_Text errorText;

    public GameObject loadingModal; // Keep this to show/hide the panel
    public LoadingTextAnimator loadingAnimator; // Add this to control the animation

    [Header("Scene Management")]
    [Tooltip("The name of the scene to load after successful authentication.")]
    public string sceneToLoadOnSuccess;

    private string _sessionPath;
    private string _sessionId;

    public string GetSessionId()
    {
        return _sessionId;
    }

    private void Awake()
    {
        Debug.Log($"WebSocketManager Awake: {gameObject.name} (scene: {gameObject.scene.name})");
        if (Instance != null && Instance != this)
        {
            Debug.Log($"Destroying duplicate WebSocketManager: {gameObject.name}");
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
        Debug.Log($"WebSocketManager instance set: {gameObject.name}");

        // Ensure we have the authenticator component assigned in the Inspector
        if (_authenticator == null)
        {
            enabled = false; // Disable this component to prevent errors
            return;
        }

        // Define the path for the temporary session file, consistent with other managers
        string debugFolder = Path.Combine(Application.dataPath, "_DebugTokens");
        if (!Directory.Exists(debugFolder))
        {
            Directory.CreateDirectory(debugFolder);
        }
        _sessionPath = Path.Combine(debugFolder, "session.dat");
    }

    private void Start()
    {
        errorModal.SetActive(false);
        if (loadingModal != null)
            loadingModal.SetActive(false); // Ensure loading modal is hidden on start
    }

    public void AuthenticateWebSocket()
    {
        AuthenticateWebSocket(null, true);
    }

    public void AuthenticateWebSocket(Action<bool> onComplete = null, bool loadSceneOnSuccess = true)
    {
        string jwt = JwtManager.Instance.GetJwt();
        string refreshToken = JwtManager.Instance.GetRefreshToken();
        string userId = PlayerManager.Instance.GetUserId();
        string deviceId = DeviceUtils.GetDeviceId();

        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(userId))
        {
            HandleAuthError("Cannot authenticate WebSocket: Missing JWT or UserID. Please log in first.");
            onComplete?.Invoke(false);
            return;
        }

        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Authenticating");

        var authRequest = new WebSocketAuthRequest
        {
            JwtToken = jwt,
            RefreshToken = refreshToken,
            UserId = userId,
            DeviceId = deviceId,
        };

        _authenticator.Authenticate(
            authRequest,
            (response) => StartCoroutine(HandleAuthResponseWithAnimation(response, onComplete, loadSceneOnSuccess))
        );
    }

    private IEnumerator HandleAuthResponseWithAnimation(
        WebSocketAuthResponse response,
        Action<bool> onComplete,
        bool loadSceneOnSuccess
    )
    {
        float minAnimationTime = 0.5f;
        float startTime = Time.time;

        float elapsedTime = Time.time - startTime;
        if (elapsedTime < minAnimationTime)
        {
            yield return new WaitForSeconds(minAnimationTime - elapsedTime);
        }

        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        if (response != null && response.Authenticated)
        {
            _sessionId = response.SessionId;
            Debug.Log($"<color=green>WebSocket authenticated successfully. Session ID: {_sessionId}</color>");

            Debug.Log("Firing OnSessionIdReady event.");
            OnSessionIdReady?.Invoke(_sessionId);

            JwtManager.Instance.SetToken(
                response.Token,
                response.RefreshToken,
                PlayerManager.Instance.GetUserId(),
                JwtManager.ParseJwtExpiry(response.Token)
            );

            try
            {
                File.WriteAllText(_sessionPath, _sessionId);
                Debug.Log($"Session ID saved to: {_sessionPath}");
            }
            catch (Exception ex)
            {
                HandleAuthError($"Failed to save session ID: {ex.Message}");
                onComplete?.Invoke(false);
                yield break;
            }

            

            if (loadSceneOnSuccess)
            {
                if (!string.IsNullOrEmpty(sceneToLoadOnSuccess))
                {
                    SceneManager.LoadScene(sceneToLoadOnSuccess);
                }
            }
            onComplete?.Invoke(true);
        }
        else
        {
            JwtManager.Instance.ClearToken();
            string reason = response?.Reason ?? "An unknown authentication error occurred.";
            Debug.LogError($"WebSocket Authentication Failed: {reason}");
            HandleAuthError($"WebSocket Authentication Failed: {reason}");
            onComplete?.Invoke(false);
        }
    }

    private void HandleAuthError(string errorMessage)
    {
        Debug.LogError(errorMessage, this);

        if (errorModal != null && errorText != null)
        {
            errorText.text = errorMessage;
            errorModal.SetActive(true);
        }
    }
}
