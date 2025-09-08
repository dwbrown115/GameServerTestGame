using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent service that fetches and caches the list of shop skins and saves the payload to disk as JSON.
/// Designed for reuse on the initialization screen and shop screen.
/// </summary>
public class SkinsService : MonoBehaviour
{
    public static SkinsService Instance { get; private set; }

    [Header("API Settings")]
    [SerializeField]
    private string apiBaseUrl = "https://localhost:7123";

    [SerializeField]
    private string skinsPath = "/api/Shop/skins";

    [Tooltip("Start polling automatically on Awake.")]
    [SerializeField]
    private bool autoStart = false;

    [Tooltip("Polling interval in seconds when autoStart is enabled.")]
    [SerializeField]
    private float pollIntervalSeconds = 60f;

    [Tooltip("Fetch once automatically on Start (no polling). Useful for page-start refresh.")]
    [SerializeField]
    private bool fetchOnceOnStart = true;

    [Tooltip("Automatically fetch once when a new scene loads (no polling).")]
    [SerializeField]
    private bool fetchOnSceneLoad = true;

    [Tooltip("Minimum seconds between auto fetches to avoid rapid refetch on quick scene swaps.")]
    [SerializeField]
    private float minSecondsBetweenAutoFetches = 5f;

    [Tooltip("If true, include Authorization header (Bearer JWT) if available.")]
    [SerializeField]
    private bool includeAuthHeader = false;

    [Tooltip("Load cached payload JSON from disk on Awake and publish it immediately if present.")]
    [SerializeField]
    private bool loadCachedOnAwake = true;

    public event Action OnSkinsUpdated;
    public event Action<string> OnError;

    [Serializable]
    public class SkinItem
    {
        public string SkinId;
        public string HexValue;
        public int Price;
    }

    [Serializable]
    private class SkinsApiResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType { get; set; }

        [JsonProperty("payload")]
        public List<SkinItem> Payload { get; set; }
    }

    private readonly List<SkinItem> _skins = new List<SkinItem>();
    private Coroutine _pollCoroutine;
    private bool _isFetching;
    private bool _initialized;
    private float _lastFetchTime;
    private float _lastAutoFetchRealtime;

    private string _cacheDir;
    private string _cachePath;

    public IReadOnlyList<SkinItem> Skins => _skins;
    public bool HasData => _skins.Count > 0;
    public float LastFetchTime => _lastFetchTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _cacheDir = Path.Combine(Application.dataPath, "_DebugTokens");
        _cachePath = Path.Combine(_cacheDir, "skins_payload.json");

        if (loadCachedOnAwake)
        {
            var cached = LoadPayloadFromDisk();
            if (cached != null && cached.Count > 0)
            {
                _skins.Clear();
                _skins.AddRange(cached);
                _initialized = true;
                _lastFetchTime = Time.unscaledTime;
                OnSkinsUpdated?.Invoke();
            }
        }

        if (autoStart)
        {
            StartPolling();
        }
    }

    private void Start()
    {
        if (fetchOnceOnStart)
        {
            ForceRefresh();
        }
    }

    private void OnEnable()
    {
        // Listen for scene loads so we can fetch once per page automatically if desired
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!fetchOnSceneLoad)
            return;
        // Avoid rapid refetch if scenes switch quickly
        float now = Time.realtimeSinceStartup;
        if (now - _lastAutoFetchRealtime < minSecondsBetweenAutoFetches)
            return;
        _lastAutoFetchRealtime = now;
        ForceRefresh();
    }

    private void OnApplicationQuit()
    {
        DeletePayloadCache();
    }

    public void Configure(
        string baseUrl = null,
        string path = null,
        float? pollInterval = null,
        bool? authHeader = null
    )
    {
        if (!string.IsNullOrEmpty(baseUrl))
            apiBaseUrl = baseUrl;
        if (!string.IsNullOrEmpty(path))
            skinsPath = path;
        if (pollInterval.HasValue)
            pollIntervalSeconds = Mathf.Max(5f, pollInterval.Value);
        if (authHeader.HasValue)
            includeAuthHeader = authHeader.Value;
    }

    public void StartPolling()
    {
        if (_pollCoroutine != null)
            StopCoroutine(_pollCoroutine);
        _pollCoroutine = StartCoroutine(PollLoop());
    }

    public void StopPolling()
    {
        if (_pollCoroutine != null)
            StopCoroutine(_pollCoroutine);
        _pollCoroutine = null;
    }

    public void ForceRefresh()
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(FetchOnce());
    }

    private IEnumerator PollLoop()
    {
        yield return FetchOnce();
        while (true)
        {
            yield return new WaitForSeconds(pollIntervalSeconds);
            yield return FetchOnce();
        }
    }

    private IEnumerator FetchOnce()
    {
        if (_isFetching)
            yield break;
        _isFetching = true;
        string url = apiBaseUrl.TrimEnd('/') + skinsPath;

        var headers = new Dictionary<string, string>();
        if (includeAuthHeader && JwtManager.Instance != null)
        {
            string token = JwtManager.Instance.GetJwt();
            if (!string.IsNullOrEmpty(token))
            {
                headers["Authorization"] = "Bearer " + token;
            }
        }

        yield return Net.HttpRequest.Send(
            "GET",
            url,
            resp =>
            {
                if (resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<SkinsApiResponse>(resp.body);
                        if (response?.Payload == null)
                        {
                            RaiseError("Malformed skins response.");
                        }
                        else
                        {
                            if (
                                !string.IsNullOrEmpty(response.ResponseType)
                                && response.ResponseType != "skins_data_response"
                            )
                            {
                                Debug.LogWarning(
                                    $"SkinsService: Unexpected response_type {response.ResponseType}"
                                );
                            }
                            UpdateCache(response.Payload);
                            // Save only the payload as JSON, as requested
                            SavePayloadToDisk(response.Payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        RaiseError("Parse failure: " + ex.Message);
                    }
                }
                else
                {
                    RaiseError($"HTTP {resp.statusCode}: {resp.error}");
                }
            },
            null,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );

        _isFetching = false;
    }

    private void UpdateCache(List<SkinItem> newSkins)
    {
        // Optional: sort by price or any criteria; keep order as-is for now
        bool changed = !_initialized || newSkins.Count != _skins.Count;
        if (!changed)
        {
            for (int i = 0; i < newSkins.Count; i++)
            {
                if (
                    newSkins[i].SkinId != _skins[i].SkinId
                    || newSkins[i].HexValue != _skins[i].HexValue
                    || newSkins[i].Price != _skins[i].Price
                )
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            _skins.Clear();
            _skins.AddRange(newSkins);
            _lastFetchTime = Time.unscaledTime;
            _initialized = true;
            OnSkinsUpdated?.Invoke();
        }
    }

    private void SavePayloadToDisk(List<SkinItem> payload)
    {
        try
        {
            if (!Directory.Exists(_cacheDir))
                Directory.CreateDirectory(_cacheDir);
            string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            File.WriteAllText(_cachePath, json);
            Debug.Log($"SkinsService: Saved payload to {_cachePath} ({json.Length} chars)");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SkinsService: Failed to save payload to disk: {ex.Message}");
        }
    }

    public List<SkinItem> LoadPayloadFromDisk()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return null;
            string json = File.ReadAllText(_cachePath);
            var list = JsonConvert.DeserializeObject<List<SkinItem>>(json);
            return list;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SkinsService: Failed to load payload from disk: {ex.Message}");
            return null;
        }
    }

    private void DeletePayloadCache()
    {
        try
        {
            if (!string.IsNullOrEmpty(_cachePath) && File.Exists(_cachePath))
            {
                File.Delete(_cachePath);
                Debug.Log($"SkinsService: Deleted {_cachePath}");
            }
            string metaPath = _cachePath + ".meta";
            if (!string.IsNullOrEmpty(metaPath) && File.Exists(metaPath))
            {
                File.Delete(metaPath);
                Debug.Log($"SkinsService: Deleted {metaPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SkinsService: Failed to delete cache on quit: {ex.Message}");
        }
    }

    private void RaiseError(string message)
    {
        Debug.LogError("SkinsService: " + message);
        OnError?.Invoke(message);
    }
}
