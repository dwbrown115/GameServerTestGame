using System.Collections;
using TMPro; // Added for TMP_Text
using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartGame : MonoBehaviour
{
    public PlayerController2D playerController2D; // Reference to the PlayerController2D
    private WebSocketManager websocketManager;

    [Header("UI Elements")]
    public GameObject loadingModal;
    public LoadingTextAnimator loadingAnimator;
    public GameObject errorModal;
    public TMP_Text errorText;

    private void Start()
    {
        websocketManager = WebSocketManager.Instance;
        if (loadingModal != null)
            loadingModal.SetActive(false);
        if (errorModal != null)
            errorModal.SetActive(false);
    }

    public void RestartCurrentScene()
    {
        StartCoroutine(RestartCurrentSceneCoroutine());
    }

    private IEnumerator RestartCurrentSceneCoroutine()
    {
        Debug.Log("RestartCurrentSceneCoroutine started.");
        Time.timeScale = 1f;

        if (websocketManager == null)
        {
            // Attempt to find the instance again, in case Start() hasn't run yet for some reason.
            websocketManager = WebSocketManager.Instance;
            if (websocketManager == null)
            {
                HandleError("WebSocketManager instance not found.");
                yield break;
            }
        }

        if (loadingModal != null)
            loadingModal.SetActive(true);
        if (loadingAnimator != null)
            loadingAnimator.StartAnimation("Starting New Session");

        bool authCompleted = false;
        bool authSuccess = false;
        websocketManager.AuthenticateWebSocket(
            (success) =>
            {
                authSuccess = success;
                authCompleted = true;
            },
            false // Do not load scene on success
        );

        yield return new WaitUntil(() => authCompleted);

        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
            loadingModal.SetActive(false);

        if (authSuccess)
        {
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
