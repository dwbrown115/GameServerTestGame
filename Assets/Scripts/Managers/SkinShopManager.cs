using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manager responsible for interacting with the skin shop / cosmetic APIs.
/// First feature: fetch the player's currently active skin after JWT verification.
/// </summary>
public class SkinShopManager : MonoBehaviour
{
    public static SkinShopManager Instance { get; private set; }

    [Header("API Settings")]
    [SerializeField]
    private string apiBaseUrl = "https://localhost:7123"; // Keep consistent with other managers

    [Tooltip("Relative path for active skin endpoint (user inferred from JWT).")]
    [SerializeField]
    private string activeSkinPath = "/api/Shop/active-skin";

    [Tooltip("Automatically fetch active skin immediately after JWT is verified.")]
    [SerializeField]
    private bool autoFetchAfterAuth = true;

    [Tooltip(
        "Delay (seconds) after auth before first active skin fetch (to ensure other data loads first)."
    )]
    [SerializeField]
    private float fetchDelaySeconds = 1.5f;

    [Tooltip("Include Authorization header (Bearer JWT)")]
    [SerializeField]
    private bool includeAuthHeader = true;

    [Header("Retry Settings")]
    [Tooltip(
        "If the first attempt returns 403, retry after a delay (may happen if auth propagation is slow)."
    )]
    [SerializeField]
    private bool retryOnForbidden = true;

    [SerializeField]
    private float forbiddenRetryDelaySeconds = 2f;

    [SerializeField]
    private int maxForbiddenRetries = 2;

    [Header("Debug / Diagnostics")]
    [Tooltip("Log detailed step-by-step info during active skin requests.")]
    [SerializeField]
    private bool verboseLogging = true;

    private Coroutine _inFlightRequest;
    private Coroutine _autoFetchCoroutine;
    private int _forbiddenRetryCount;

    [Serializable]
    public class ActiveSkinResponse
    {
        public string skinId;
        public string skinName;
        public string rarity;
        public string equippedAt;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to auth state changes so we can trigger after verification
        JwtManager.OnAuthStateChanged += HandleAuthStateChanged;

        // No immediate fetch here; defer to Start() / auth event so we can apply delay.
    }

    private void Start()
    {
        if (autoFetchAfterAuth && JwtManager.Instance != null && JwtManager.Instance.IsTokenValid())
        {
            ScheduleDeferredFetch();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            JwtManager.OnAuthStateChanged -= HandleAuthStateChanged;
        }
    }

    private void HandleAuthStateChanged(bool isAuthed)
    {
        if (!autoFetchAfterAuth)
            return;
        if (isAuthed && JwtManager.Instance != null && JwtManager.Instance.IsTokenValid())
        {
            ScheduleDeferredFetch();
        }
    }

    private void ScheduleDeferredFetch()
    {
        if (_autoFetchCoroutine != null)
            StopCoroutine(_autoFetchCoroutine);
        _autoFetchCoroutine = StartCoroutine(DeferredFetchCoroutine());
    }

    private IEnumerator DeferredFetchCoroutine()
    {
        if (fetchDelaySeconds > 0f)
            yield return new WaitForSeconds(fetchDelaySeconds);
        TryFetchActiveSkin();
    }

    /// <summary>
    /// Public entry to manually refetch the active skin.
    /// </summary>
    public void TryFetchActiveSkin()
    {
        if (_inFlightRequest != null)
        {
            // Prevent spamming; could be extended with cancellation if desired.
            LogVerbose("TryFetchActiveSkin aborted: request already in flight.");
            return;
        }
        _forbiddenRetryCount = 0; // reset on a manual or scheduled new attempt
        LogVerbose("TryFetchActiveSkin starting.");
        if (JwtManager.Instance == null)
        {
            Debug.LogWarning("SkinShopManager: Cannot fetch active skin - JwtManager missing.");
            return;
        }
        bool tokenValid = JwtManager.Instance.IsTokenValid();
        LogVerbose($"JWT validity check: {tokenValid}");
        if (!tokenValid)
        {
            Debug.LogWarning("SkinShopManager: Cannot fetch active skin - invalid/expired JWT.");
            return;
        }
        // (Optional) we could wait until PlayerManager has a userId if needed, but endpoint no longer requires it.
        string url = apiBaseUrl.TrimEnd('/') + activeSkinPath;
        LogVerbose($"Constructed URL: {url}");
        _inFlightRequest = StartCoroutine(FetchActiveSkin(url));
    }

    private IEnumerator FetchActiveSkin(string url)
    {
        LogVerbose("---- Active Skin Fetch BEGIN ----");
        LogVerbose($"Timestamp (UTC): {DateTime.UtcNow:O}");
        LogVerbose($"GET {url}");
        DateTime startTs = DateTime.UtcNow;
        using (var request = UnityWebRequest.Get(url))
        {
            if (includeAuthHeader && JwtManager.Instance != null)
            {
                string raw = JwtManager.Instance.GetJwt();
                string norm = NormalizeToken(raw);
                if (!string.IsNullOrEmpty(norm))
                {
                    string headerValue = "Bearer " + norm;
                    LogVerbose(
                        $"Preparing Authorization header. RawLen={(raw ?? "null").Length} NormalizedLen={norm.Length} Header='Authorization: Bearer <token>'"
                    );
                    LogVerbose($"Masked token: {MaskToken(norm)}");
                    request.SetRequestHeader("Authorization", headerValue);
                }
                else
                {
                    LogVerbose(
                        "JWT empty/whitespace after normalization; Authorization header NOT set (will 401)."
                    );
                }
            }
            request.certificateHandler = PlayerApiCertificateHandler.Instance;
            LogVerbose("Assigned custom certificate handler.");
            request.timeout = 10;
            LogVerbose("Request timeout set to 10s. Sending...");
            yield return request.SendWebRequest();
            double ms = (DateTime.UtcNow - startTs).TotalMilliseconds;
            LogVerbose(
                $"Request completed in {ms:F1} ms. Result={request.result} Code={request.responseCode}"
            );
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;
            LogVerbose($"Raw body length={(body != null ? body.Length : 0)}");
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"SkinShopManager: Active skin raw response: {Truncate(body, 400)}");
                // Parse minimal fields for persistence
                try
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ActiveSkinResponse>(
                        body
                    );
                    if (parsed != null)
                    {
                        LogVerbose(
                            $"Parsed skinId={parsed.skinId} hex={parsed.skinName ?? parsed.rarity}"
                        );
                        // The API spec mentioned SkinId and HexValue; adapt fields if hex is under a different key
                        string skinId = parsed.skinId;
                        // Attempt to find a hex value in known fields; if API uses hexValue, we need the concrete model
                        string hex = ExtractHexValueFromJson(body);
                        if (!string.IsNullOrEmpty(skinId) || !string.IsNullOrEmpty(hex))
                        {
                            PlayerManager.Instance?.SetActiveSkin(skinId, hex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"SkinShopManager: Failed to parse active skin JSON: {ex.Message}"
                    );
                }
                _forbiddenRetryCount = 0; // success resets
            }
            else
            {
                long code = request.responseCode;
                Debug.LogError($"SkinShopManager: HTTP {code} error: {request.error}");
                if (code == 401)
                {
                    LogVerbose(
                        "Received 401 Unauthorized. Possible causes: token expired, clock skew, server not recognizing token yet."
                    );
                    if (JwtManager.Instance != null)
                    {
                        string raw = JwtManager.Instance.GetJwt();
                        string norm = NormalizeToken(raw);
                        string masked = MaskToken(norm);
                        LogVerbose(
                            $"Current JWT rawLen={(raw ?? "null").Length} normLen={(norm ?? "null")?.Length} masked={masked}"
                        );
                        DateTime expiry = JwtManager.Instance.GetExpiry();
                        LogVerbose(
                            $"Token expiry (UTC): {expiry:O}; Now (UTC): {DateTime.UtcNow:O}; Delta seconds: {(expiry - DateTime.UtcNow).TotalSeconds:F1}"
                        );
                        if (string.IsNullOrEmpty(norm))
                            LogVerbose("Normalized token empty - header likely omitted.");
                    }
                }
                if (
                    code == 403
                    && retryOnForbidden
                    && _forbiddenRetryCount < maxForbiddenRetries
                    && JwtManager.Instance != null
                    && JwtManager.Instance.IsTokenValid()
                )
                {
                    _forbiddenRetryCount++;
                    Debug.LogWarning(
                        $"SkinShopManager: Received 403; scheduling retry #{_forbiddenRetryCount} in {forbiddenRetryDelaySeconds} sec."
                    );
                    yield return new WaitForSeconds(forbiddenRetryDelaySeconds);
                    _inFlightRequest = StartCoroutine(FetchActiveSkin(url));
                    LogVerbose("---- Active Skin Fetch RETRY scheduled ----");
                    yield break; // exit this instance; retry coroutine continues
                }
            }
        }
        LogVerbose("---- Active Skin Fetch END ----");
        _inFlightRequest = null;
    }

    /// <summary>
    /// Call this when the player presses Play: re-fetch active skin and compare to stored values.
    /// </summary>
    public void ValidateActiveSkinAgainstSaved()
    {
        StartCoroutine(ValidateCoroutine());
    }

    private IEnumerator ValidateCoroutine()
    {
        // Fetch latest (reusing TryFetchActiveSkin flow but we want to compare results)
        if (JwtManager.Instance == null || !JwtManager.Instance.IsTokenValid())
            yield break;
        string url = apiBaseUrl.TrimEnd('/') + activeSkinPath;
        using (var request = UnityWebRequest.Get(url))
        {
            string raw = JwtManager.Instance.GetJwt();
            string norm = NormalizeToken(raw);
            if (!string.IsNullOrEmpty(norm))
                request.SetRequestHeader("Authorization", "Bearer " + norm);
            request.certificateHandler = PlayerApiCertificateHandler.Instance;
            request.timeout = 10;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string body = request.downloadHandler.text;
                string latestSkinId = null;
                string latestHex = null;
                try
                {
                    var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<ActiveSkinResponse>(
                        body
                    );
                    latestSkinId = parsed?.skinId;
                    latestHex = ExtractHexValueFromJson(body);
                }
                catch { }
                string savedSkinId = PlayerManager.Instance?.GetSavedSkinId();
                string savedHex = PlayerManager.Instance?.GetSavedSkinHex();
                if (latestSkinId != savedSkinId || latestHex != savedHex)
                {
                    Debug.LogWarning(
                        $"Skin mismatch: saved({savedSkinId},{savedHex}) vs latest({latestSkinId},{latestHex}). Updating save."
                    );
                    PlayerManager.Instance?.SetActiveSkin(latestSkinId, latestHex);
                }
                else
                {
                    LogVerbose("Active skin matches saved data.");
                }
            }
            else
            {
                Debug.LogWarning(
                    $"ValidateActiveSkinAgainstSaved: HTTP {request.responseCode} {request.error}"
                );
            }
        }
    }

    private string ExtractHexValueFromJson(string json)
    {
        try
        {
            var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
            // Prefer explicit hexValue if provided
            var hex = (string)jo["hexValue"] ?? (string)jo["HexValue"];
            if (!string.IsNullOrEmpty(hex))
                return hex;
            // Fallback: sometimes color may be nested, customize as needed
            // e.g., jo["color"]["hex"]
            var color = jo["color"] as Newtonsoft.Json.Linq.JObject;
            if (color != null)
            {
                hex = (string)color["hex"] ?? (string)color["Hex"];
                if (!string.IsNullOrEmpty(hex))
                    return hex;
            }
        }
        catch { }
        return null;
    }

    private void LogVerbose(string msg)
    {
        if (!verboseLogging)
            return;
        Debug.Log("[SkinShopManager][Verbose] " + msg);
    }

    private string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return "<empty>";
        int show = Math.Min(12, token.Length / 3);
        if (show <= 0)
            show = Math.Min(4, token.Length);
        string prefix = token.Substring(0, show);
        string suffix = token.Substring(token.Length - show, show);
        return prefix + "..." + suffix + $" (len={token.Length})";
    }

    private string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s.Substring(0, max) + "...<truncated>";
    }

    private string NormalizeToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string trimmed = raw.Trim();
        // If token already has a Bearer prefix (e.g., accidentally stored) strip it.
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(7).Trim();
        }
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
