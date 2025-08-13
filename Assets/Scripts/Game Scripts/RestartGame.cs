using System.Collections;
using System.IO; // Added for File operations
using TMPro; // Added for TMP_Text
using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartGame : MonoBehaviour
{
    public PlayerController2D playerController2D; // Reference to the PlayerController2D
    public WebSocketManager websocketManager; // Reference to the WebSocketManager

    [Header("UI Elements")]
    public GameObject loadingModal;
    public LoadingTextAnimator loadingAnimator;
    public GameObject errorModal;
    public TMP_Text errorText;

    private void Start()
    {
        if (loadingModal != null)
            loadingModal.SetActive(false);
        if (errorModal != null)
            errorModal.SetActive(false);
    }

    /// <summary>
    /// Restarts the current active scene and re-establishes WebSocket connection.
    /// This method is intended to be called from a UI Button's OnClick event.
    /// </summary>
    public void RestartCurrentScene()
    {
        StartCoroutine(RestartCurrentSceneCoroutine());
    }

    private IEnumerator RestartCurrentSceneCoroutine()
    {
        Debug.Log("Restarting the current scene and WebSocket session...");
        Time.timeScale = 1f;

        if (websocketManager == null)
        {
            HandleError("WebSocketManager reference not set in RestartGame script.");
            yield break;
        }

        // Show loading modal
        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Starting New Session");

        // 1. Delete the old session.dat file to force a new session ID
        string debugFolder = Path.Combine(Application.dataPath, "_DebugTokens");
        string sessionPath = Path.Combine(debugFolder, "session.dat");
        if (File.Exists(sessionPath))
        {
            try
            {
                File.Delete(sessionPath);
                Debug.Log("Old session.dat deleted.");
            }
            catch (System.Exception ex)
            {
                HandleError($"Failed to delete old session.dat: {ex.Message}");
                yield break;
            }
        }

        // 2. Authenticate WebSocket to get a new session ID
        bool authSuccess = false;
        websocketManager.AuthenticateWebSocket(
            (success) =>
            {
                authSuccess = success;
            }
        );

        // Wait for authentication to complete
        yield return new WaitUntil(() => authSuccess);

        // Hide loading modal
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        if (authSuccess)
        {
            Debug.Log("New WebSocket session authenticated successfully. Reloading scene.");
            // Reload the current scene.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            HandleError("Failed to authenticate new WebSocket session. Cannot restart game.");
        }
    }

    private void HandleError(string errorMessage)
    {
        Debug.LogError(errorMessage);
        if (errorModal != null && errorText != null)
        {
            errorText.text = errorMessage;
            errorModal.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "Cannot display error modal. UI elements not assigned in the Inspector."
            );
        }
    }
}
