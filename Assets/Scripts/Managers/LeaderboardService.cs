using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Persistent singleton that fetches and caches leaderboard data early (e.g., from Initialization scene) and
/// periodically refreshes it. UI layers (e.g., LeaderboardManager / view) subscribe to OnEntriesUpdated.
/// </summary>
public class LeaderboardService : MonoBehaviour
{
    public static LeaderboardService Instance { get; private set; }

    [Header("API Settings")]
    [SerializeField]
    private string apiBaseUrl = "https://localhost:7123";

    [SerializeField]
    private string leaderboardPath = "/api/Leaderboard";

    [SerializeField]
    private float pollIntervalSeconds = 30f;

    [Tooltip("Start polling automatically on Awake.")]
    [SerializeField]
    private bool autoStart = true;

    [Tooltip(
        "If true, include Authorization header (Bearer JWT). Leave false for public leaderboard."
    )]
    [SerializeField]
    private bool includeAuthHeader = false;

    public event Action OnEntriesUpdated;
    public event Action<string> OnError;

    private readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();
    private Coroutine _pollCoroutine;
    private bool _isFetching;
    private float _lastFetchTime;
    private bool _initialized;

    public IReadOnlyList<LeaderboardEntry> Entries => _entries;
    public bool HasData => _entries.Count > 0;
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
        if (autoStart)
            StartPolling();
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
            leaderboardPath = path;
        if (pollInterval.HasValue)
            pollIntervalSeconds = Mathf.Max(5f, pollInterval.Value);
        if (authHeader.HasValue)
            includeAuthHeader = authHeader.Value;
    }

    public void SetIncludeAuthHeader(bool enabled) => includeAuthHeader = enabled;

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
        // Initial eager fetch
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
        string url = apiBaseUrl.TrimEnd('/') + leaderboardPath;
        using (var request = UnityWebRequest.Get(url))
        {
            if (includeAuthHeader)
            {
                string token = JwtManager.Instance != null ? JwtManager.Instance.GetJwt() : null;
                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                }
            }
            request.certificateHandler = PlayerApiCertificateHandler.Instance;
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                Debug.Log($"LeaderboardService: Raw JSON response: {json}");
                try
                {
                    var response = JsonConvert.DeserializeObject<LeaderboardApiResponse>(json);
                    if (response?.Payload == null)
                    {
                        RaiseError("Malformed leaderboard response.");
                    }
                    else
                    {
                        if (
                            !string.IsNullOrEmpty(response.ResponseType)
                            && response.ResponseType != "leaderboard_data_response"
                        )
                        {
                            Debug.LogWarning(
                                $"LeaderboardService: Unexpected response_type {response.ResponseType}"
                            );
                        }
                        UpdateCache(response.Payload);
                    }
                }
                catch (Exception ex)
                {
                    RaiseError("Parse failure: " + ex.Message);
                }
            }
            else
            {
                RaiseError($"HTTP {request.responseCode}: {request.error}");
            }
        }
        _isFetching = false;
    }

    private void UpdateCache(List<LeaderboardEntry> newEntries)
    {
        // Sort descending by score
        newEntries.Sort((a, b) => b.PlayerHighestScore.CompareTo(a.PlayerHighestScore));

        bool changed = !_initialized || newEntries.Count != _entries.Count;
        if (!changed)
        {
            for (int i = 0; i < newEntries.Count; i++)
            {
                if (
                    newEntries[i].Username != _entries[i].Username
                    || newEntries[i].PlayerHighestScore != _entries[i].PlayerHighestScore
                )
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            _entries.Clear();
            _entries.AddRange(newEntries);
            _lastFetchTime = Time.unscaledTime;
            _initialized = true;
            OnEntriesUpdated?.Invoke();
        }
    }

    private void RaiseError(string message)
    {
        Debug.LogError("LeaderboardService: " + message);
        OnError?.Invoke(message);
    }
}
