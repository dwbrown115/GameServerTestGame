using System;
using System.IO;
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
            Debug.LogError(
                "WebSocketAuthenticator is not assigned in the Inspector. Please assign it.",
                this
            );
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
    }

    /// <summary>
    /// Gathers user credentials and initiates the WebSocket authentication process.
    /// </summary>
    public void AuthenticateWebSocket()
    {
        // 1. Retrieve credentials from the respective managers.
        string jwt = JwtManager.Instance.GetJwt();
        string refreshToken = JwtManager.Instance.GetRefreshToken();
        string userId = PlayerManager.Instance.GetUserId();
        string deviceId = DeviceUtils.GetDeviceId();

        // 2. Validate that we have the essential credentials before proceeding.
        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(userId))
        {
            HandleAuthError(
                "Cannot authenticate WebSocket: Missing JWT or UserID. Please log in first."
            );
            return;
        }

        Debug.Log("Attempting WebSocket authentication...");

        // 3. Create the authentication request payload.
        var authRequest = new WebSocketAuthRequest
        {
            JwtToken = jwt,
            RefreshToken = refreshToken,
            UserId = userId,
            DeviceId = deviceId,
        };

        // 4. Send the request and provide a callback to handle the response.
        _authenticator.Authenticate(authRequest, HandleAuthResponse);
    }

    /// <summary>
    /// Handles the response from the WebSocketAuthenticator.
    /// </summary>
    /// <param name="response">The authentication response from the server.</param>
    private void HandleAuthResponse(WebSocketAuthResponse response)
    {
        Console.WriteLine("WebSocket Auth Response: " + JsonUtility.ToJson(response));
        if (response != null && response.Authenticated)
        {
            // Success case: Store the session ID.
            _sessionId = response.SessionId;
            Debug.Log(
                $"<color=green>WebSocket authenticated successfully. Session ID: {_sessionId}</color>"
            );

            try
            {
                File.WriteAllText(_sessionPath, _sessionId);
                Debug.Log($"Session ID saved to: {_sessionPath}");
            }
            catch (Exception ex)
            {
                HandleAuthError($"Failed to save session ID: {ex.Message}");
                return; // Do not proceed if we can't save the session
            }

            // Load the next scene on success
            if (!string.IsNullOrEmpty(sceneToLoadOnSuccess))
            {
                Debug.Log($"Authentication successful. Loading scene: {sceneToLoadOnSuccess}");
                SceneManager.LoadScene(sceneToLoadOnSuccess);
            }
            else
            {
                Debug.LogWarning("sceneToLoadOnSuccess is not set. Remaining on current scene.");
            }
        }
        else
        {
            // Failure case: Display the error.
            string reason = response?.Reason ?? "An unknown authentication error occurred.";
            HandleAuthError($"WebSocket Authentication Failed: {reason}");
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
            Debug.LogWarning(
                "Cannot display error modal. UI elements not assigned in the Inspector.",
                this
            );
        }
    }
}
