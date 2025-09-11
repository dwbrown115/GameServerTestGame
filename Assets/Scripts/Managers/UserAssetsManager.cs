using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Fetches the user's assets (points and owned skins) and persists them via PlayerManager.
/// </summary>
public class UserAssetsManager : MonoBehaviour
{
    public static UserAssetsManager Instance { get; private set; }
    private const string LOG_TAG = "[UserAssets]";

    [Header("API Settings")]
    [SerializeField]
    private string apiBaseUrl = "https://localhost:7123";

    [Tooltip("Relative path template for user assets endpoint. {userId} will be replaced.")]
    [SerializeField]
    private string userAssetsPath = "/api/Shop/user-assets/{userId}";

    [Tooltip("Fetch on Start if JWT + userId available.")]
    [SerializeField]
    private bool fetchOnStart = true;

    [Tooltip("Include Authorization header (Bearer JWT) if available.")]
    [SerializeField]
    private bool includeAuthHeader = true;

    [Header("Debug")]
    [SerializeField]
    private bool verbose = true;

    // Deprecated typed models removed; we now parse flexibly to support multiple server shapes

    public event Action OnAssetsUpdated;
    public event Action<string> OnError;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (fetchOnStart)
            TryFetchUserAssets();
    }

    public void TryFetchUserAssets()
    {
        string userId = PlayerManager.Instance?.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            if (verbose)
                Debug.LogWarning(LOG_TAG + " Missing userId; skipping fetch.");
            return;
        }
        if (
            includeAuthHeader
            && (JwtManager.Instance == null || !JwtManager.Instance.IsTokenValid())
        )
        {
            if (verbose)
                Debug.LogWarning(LOG_TAG + " Missing/invalid JWT; skipping fetch.");
            return;
        }
        string path = userAssetsPath.Replace("{userId}", Uri.EscapeDataString(userId));
        string url = apiBaseUrl.TrimEnd('/') + path;
        var headers = new Dictionary<string, string>();
        if (includeAuthHeader)
        {
            string token = JwtManager.Instance?.GetJwt();
            if (!string.IsNullOrEmpty(token))
                headers["Authorization"] = "Bearer " + token;
        }
        StartCoroutine(Fetch(url, headers));
    }

    private IEnumerator Fetch(string url, Dictionary<string, string> headers)
    {
        if (verbose)
            Debug.Log(LOG_TAG + $" GET {url}");
        yield return Net.HttpRequest.Send(
            "GET",
            url,
            resp =>
            {
                if (resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        // Some responses may have response_type/payload wrapper; handle both
                        if (verbose)
                            Debug.Log(LOG_TAG + " Response body: " + resp.body);
                        int points = 0;
                        List<string> owned = null;
                        var root = JToken.Parse(resp.body);
                        if (root.Type == JTokenType.Object)
                        {
                            var jo = (JObject)root;
                            // Support wrapper under 'payload' (snake/camel), else read from root
                            var payload = (jo["payload"] as JObject) ?? (jo["Payload"] as JObject);
                            var source = payload ?? jo;
                            // Points: support 'points' and 'Points'
                            points = (int?)source["points"] ?? (int?)source["Points"] ?? 0;
                            // Owned: support camelCase, PascalCase, and legacy keys
                            var ownedToken =
                                source["ownedSkinIds"]
                                ?? source["OwnedSkinIds"]
                                ?? source["OwnedSkins"]
                                ?? source["ownedSkins"]
                                ?? source["Owned Skins"]
                                ?? source["owned"]
                                ?? source["skins"];
                            owned = ExtractOwnedIds(ownedToken);
                            if (verbose)
                                Debug.Log(
                                    LOG_TAG
                                        + $" Parsed {(payload != null ? "via wrapper" : "from root")} -> Points={points} OwnedCount={(owned == null ? 0 : owned.Count)}"
                                );
                        }
                        else if (root.Type == JTokenType.Array)
                        {
                            // Body is the owned skins array itself
                            owned = ExtractOwnedIds(root);
                            if (verbose)
                                Debug.Log(
                                    LOG_TAG
                                        + $" Parsed via top-level array -> OwnedCount={(owned == null ? 0 : owned.Count)}"
                                );
                        }
                        PlayerManager.Instance?.SetPoints(points);
                        PlayerManager.Instance?.SetOwnedSkins(owned?.ToArray());
                        if (verbose)
                            Debug.Log(
                                LOG_TAG + $" Saved -> Points={points}, Owned={(owned?.Count ?? 0)}"
                            );
                        OnAssetsUpdated?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(LOG_TAG + $" Parse error {ex.Message}");
                        OnError?.Invoke($"User assets parse error: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogError(LOG_TAG + $" HTTP {resp.statusCode} {resp.error}");
                    OnError?.Invoke($"User assets HTTP {resp.statusCode}: {resp.error}");
                }
            },
            null,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );
    }

    private List<string> ExtractOwnedIds(JToken token)
    {
        if (token == null)
            return null;
        var result = new List<string>();
        if (token.Type == JTokenType.Array)
        {
            foreach (var el in token as JArray)
            {
                switch (el.Type)
                {
                    case JTokenType.String:
                        var s = el.Value<string>();
                        if (!string.IsNullOrEmpty(s))
                            result.Add(s);
                        break;
                    case JTokenType.Object:
                        var id = GetIdFromObject(el as JObject);
                        if (!string.IsNullOrEmpty(id))
                            result.Add(id);
                        break;
                }
            }
        }
        else if (token.Type == JTokenType.Object)
        {
            // Some APIs might return an object wrapping an array under a key
            // Try common keys
            var obj = token as JObject;
            var arr =
                obj["Owned Skins"] as JArray
                ?? obj["OwnedSkinIds"] as JArray
                ?? obj["OwnedSkins"] as JArray
                ?? obj["ownedSkinIds"] as JArray
                ?? obj["ownedSkins"] as JArray
                ?? obj["owned"] as JArray;
            if (arr != null)
                return ExtractOwnedIds(arr);
            // Or treat this single object as one entry
            var single = GetIdFromObject(obj);
            if (!string.IsNullOrEmpty(single))
                result.Add(single);
        }
        return result.Count == 0 ? null : result;
    }

    private string GetIdFromObject(JObject obj)
    {
        if (obj == null)
            return null;
        // Try common casings
        return (string)(obj["SkinId"] ?? obj["skinId"] ?? obj["Id"] ?? obj["id"] ?? obj["skin_id"]);
    }
}
