using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WebSocketManager : MonoBehaviour
{
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

    private void Awake()
    {
        // Ensure we have the authenticator component assigned in the Inspector
        if (_authenticator == null)
        {
            // Debug.LogError(
            //     "WebSocketAuthenticator is not assigned in the Inspector. Please assign it.",
            //     this
            // );
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

    /// <summary>
    /// Gathers user credentials and initiates the WebSocket authentication process.
    /// This version is for UI buttons that do not provide a callback.
    /// </summary>
    public void AuthenticateWebSocket()
    {
        AuthenticateWebSocket(null); // Call the version with the optional callback
    }

    /// <summary>
    /// Gathers user credentials and initiates the WebSocket authentication process.
    /// </summary>
    public void AuthenticateWebSocket(Action<bool> onComplete = null) // Added optional onComplete callback
    {
        // 1. Retrieve credentials from the respective managers.
        string jwt = JwtManager.Instance.GetJwt();
        string refreshToken = JwtManager.Instance.GetRefreshToken();
        string userId = PlayerManager.Instance.GetUserId();
        string deviceId = DeviceUtils.GetDeviceId();

        Debug.Log(
            $"WebsocketManager: Authenticating WebSocket. JWT: {jwt}, RefreshToken: {refreshToken}, UserID: {userId}, DeviceID: {deviceId}"
        );

        // 2. Validate that we have the essential credentials before proceeding.
        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(userId))
        {
            HandleAuthError(
                "Cannot authenticate WebSocket: Missing JWT or UserID. Please log in first."
            );
            onComplete?.Invoke(false); // Invoke callback with failure
            return;
        }

        // Debug.Log("Attempting WebSocket authentication...");

        // Show the modal and start the animation
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Authenticating"); // You might want a specific message here

        // 3. Create the authentication request payload.
        var authRequest = new WebSocketAuthRequest
        {
            JwtToken = jwt,
            RefreshToken = refreshToken,
            UserId = userId,
            DeviceId = deviceId,
        };

        // 4. Send the request and provide a callback to handle the response.
        _authenticator.Authenticate(
            authRequest,
            (response) => StartCoroutine(HandleAuthResponseWithAnimation(response, onComplete)) // Pass onComplete
        );
    }

    /// <summary>
    /// Handles the response from the WebSocketAuthenticator with animation control.
    /// </summary>
    /// <param name="response">The authentication response from the server.</param>
    private IEnumerator HandleAuthResponseWithAnimation(
        WebSocketAuthResponse response,
        Action<bool> onComplete
    ) // Added onComplete
    {
        float minAnimationTime = 0.5f; // Minimum time to show the animation
        float startTime = Time.time;

        // Wait for the minimum animation time
        float elapsedTime = Time.time - startTime;
        if (elapsedTime < minAnimationTime)
        {
            yield return new WaitForSeconds(minAnimationTime - elapsedTime);
        }

        // Stop the animation and hide the modal
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        // Now process the actual response
        Console.WriteLine("WebSocket Auth Response: " + JsonConvert.SerializeObject(response));
        if (response != null && response.Authenticated)
        {
            // Success case: Store the session ID and update tokens.
            _sessionId = response.SessionId;
            Debug.Log(
                $"<color=green>WebSocket authenticated successfully. Session ID: {_sessionId}</color>"
            );

            // Update JwtManager with potentially new tokens from WebSocketAuthResponse
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
                onComplete?.Invoke(false); // Invoke callback with failure
                yield break; // Do not proceed if we can't save the session
            }

            // Load the next scene on success
            if (!string.IsNullOrEmpty(sceneToLoadOnSuccess))
            {
                // Debug.Log($"Authentication successful. Loading scene: {sceneToLoadOnSuccess}");
                SceneManager.LoadScene(sceneToLoadOnSuccess);
            }
            else
            {
                // Debug.LogWarning("sceneToLoadOnSuccess is not set. Remaining on current scene.");
            }
            onComplete?.Invoke(true); // Invoke callback with success
        }
        else
        {
            // Failure case: Display the error.
            JwtManager.Instance.ClearToken(); // Clear tokens on WebSocket authentication failure
            string reason = response?.Reason ?? "An unknown authentication error occurred.";
            HandleAuthError($"WebSocket Authentication Failed: {reason}");
            onComplete?.Invoke(false); // Invoke callback with failure
        }
    }

    /// <summary>
    /// Logs an error and displays it in the UI error modal.
    /// </summary>
    /// <param name="errorMessage">The error message to display and log.</param>
    private void HandleAuthError(string errorMessage)
    {
        Debug.LogError(errorMessage, this);

        if (errorModal != null && errorText != null)
        {
            errorText.text = errorMessage;
            errorModal.SetActive(true);
        }
        else
        {
            // Debug.LogWarning(
            //     "Cannot display error modal. UI elements not assigned in the Inspector.",
            //     this
            // );
        }
    }
}
