using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private bool _usedSkinShopLoading; // track if we proxied to SkinShopManager for loading UI
    private GameObject _runtimeLoadingOverlay; // minimal fallback when no modal is configured
    private TextMeshProUGUI _runtimeLoadingText;

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

        if (_authenticator == null)
        {
            _authenticator = gameObject.AddComponent<WebSocketAuthenticator>();
            Debug.Log(
                "WebSocketManager: No authenticator assigned, added default WebSocketAuthenticator component."
            );
        }

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
            loadingModal.SetActive(false);
    }

    public void AuthenticateWebSocket()
    {
        AuthenticateWebSocket(null, true);
    }

    public void AuthenticateWebSocket(
        Action<bool> onComplete = null,
        bool loadSceneOnSuccess = true
    )
    {
        string jwt = JwtManager.Instance.GetJwt();
        string refreshToken = JwtManager.Instance.GetRefreshToken();
        string userId = PlayerManager.Instance.GetUserId();
        string deviceId = DeviceUtils.GetDeviceId();
        Debug.Log(
            $"WebSocketManager.Authenticate: userId={(string.IsNullOrEmpty(userId) ? "<none>" : userId)} jwt={(string.IsNullOrEmpty(jwt) ? "<none>" : "<present>")} refresh={(string.IsNullOrEmpty(refreshToken) ? "<none>" : "<present>")} deviceId={deviceId}"
        );

        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(userId))
        {
            HandleAuthError(
                "Cannot authenticate WebSocket: Missing JWT or UserID. Please log in first."
            );
            onComplete?.Invoke(false);
            return;
        }

        ShowLoading("Authenticating");

        var authRequest = new WebSocketAuthRequest
        {
            JwtToken = jwt,
            RefreshToken = refreshToken,
            UserId = userId,
            DeviceId = deviceId,
        };

        _authenticator.Authenticate(
            authRequest,
            response =>
                StartCoroutine(
                    HandleAuthResponseWithAnimation(response, onComplete, loadSceneOnSuccess)
                )
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

        HideLoading();

        if (response != null && response.Authenticated)
        {
            _sessionId = response.SessionId;
            Debug.Log(
                $"<color=green>WebSocket authenticated successfully. Session ID: {_sessionId}</color>"
            );

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

            if (loadSceneOnSuccess && !string.IsNullOrEmpty(sceneToLoadOnSuccess))
            {
                SceneManager.LoadScene(sceneToLoadOnSuccess);
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

    private void ShowLoading(string message)
    {
        // Prefer locally configured modal
        if (loadingModal != null)
        {
            loadingModal.SetActive(true);
            if (loadingAnimator != null)
                loadingAnimator.StartAnimation(string.IsNullOrEmpty(message) ? "Loading" : message);
            return;
        }

        // Fallback to SkinShopManager's loading if available
        if (SkinShopManager.Instance != null && SkinShopManager.Instance.loadingModal != null)
        {
            _usedSkinShopLoading = true;
            SkinShopManager.Instance.ShowLoading(
                string.IsNullOrEmpty(message) ? "Loading" : message
            );
            return;
        }

        // Final fallback: create a minimal overlay once
        EnsureRuntimeOverlay();
        if (_runtimeLoadingOverlay != null)
        {
            _runtimeLoadingOverlay.SetActive(true);
            if (_runtimeLoadingText != null)
                _runtimeLoadingText.text = string.IsNullOrEmpty(message) ? "Loading" : message;
        }
    }

    private void HideLoading()
    {
        if (loadingAnimator != null)
            loadingAnimator.StopAnimation();
        if (loadingModal != null)
        {
            loadingModal.SetActive(false);
            return;
        }
        if (_usedSkinShopLoading && SkinShopManager.Instance != null)
        {
            SkinShopManager.Instance.HideLoading();
            _usedSkinShopLoading = false;
            return;
        }
        if (_runtimeLoadingOverlay != null)
        {
            _runtimeLoadingOverlay.SetActive(false);
        }
    }

    private void EnsureRuntimeOverlay()
    {
        if (_runtimeLoadingOverlay != null)
            return;

        // Create a simple overlay under this manager so it persists across scenes
        var canvasGO = new GameObject("RuntimeLoadingCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Int16.MaxValue; // ensure on top
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("OverlayPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var prt = panelGO.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        var img = panelGO.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.5f);

        var textGO = new GameObject("Message");
        textGO.transform.SetParent(panelGO.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(600, 120);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 36;
        tmp.text = "Loading";
        tmp.color = Color.white;

        _runtimeLoadingOverlay = canvasGO;
        _runtimeLoadingText = tmp;
        _runtimeLoadingOverlay.SetActive(false);
    }
}
