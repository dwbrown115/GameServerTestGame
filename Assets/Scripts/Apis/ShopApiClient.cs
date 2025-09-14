using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class ShopApiClient
{
    public static string ApiBaseUrl = "https://localhost:7123";

    [Serializable]
    public class ActiveSkinApiResponse
    {
        [JsonProperty("response_type")]
        public string ResponseType;

        [JsonProperty("userId")]
        public string UserId;

        [JsonProperty("skinId")]
        public string SkinId;

        [JsonProperty("hexValue")]
        public string HexValue; // optional

        [JsonProperty("status")]
        public string Status; // Ok | Bad

        [JsonProperty("message")]
        public string Message; // optional
    }

    public static IEnumerator BuySkin(
        string userId,
        string skinId,
        Action<ActiveSkinApiResponse, string> onDone
    )
    {
        string url = ApiBaseUrl.TrimEnd('/') + "/api/Shop/buy-skin";
        string body = JsonConvert.SerializeObject(new { userId, skinId });
        var headers = BuildAuthHeaders();
        yield return Net.HttpRequest.Send(
            "POST",
            url,
            resp => HandleResponse(resp, onDone),
            body,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );
    }

    public static IEnumerator SetActiveSkin(
        string userId,
        string skinId,
        Action<ActiveSkinApiResponse, string> onDone
    )
    {
        string url = ApiBaseUrl.TrimEnd('/') + "/api/Shop/active-skin";
        string body = JsonConvert.SerializeObject(new { userId, skinId });
        var headers = BuildAuthHeaders();
        yield return Net.HttpRequest.Send(
            "PUT",
            url,
            resp => HandleResponse(resp, onDone),
            body,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );
    }

    public static IEnumerator GetActiveSkin(
        string userId,
        Action<ActiveSkinApiResponse, string> onDone
    )
    {
        string url =
            ApiBaseUrl.TrimEnd('/')
            + "/api/Shop/active-skin/"
            + Uri.EscapeDataString(userId ?? string.Empty);
        var headers = BuildAuthHeaders();
        yield return Net.HttpRequest.Send(
            "GET",
            url,
            resp => HandleResponse(resp, onDone),
            null,
            headers,
            PlayerApiCertificateHandler.Instance,
            10
        );
    }

    private static void HandleResponse(
        Net.HttpResponse resp,
        Action<ActiveSkinApiResponse, string> onDone
    )
    {
        if (resp.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<ActiveSkinApiResponse>(resp.body);
                if (parsed == null)
                {
                    onDone?.Invoke(null, "Malformed response");
                    return;
                }
                // Normalize status casing
                if (!string.IsNullOrEmpty(parsed.Status))
                    parsed.Status = parsed.Status.Trim();
                onDone?.Invoke(parsed, null);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(null, "Parse error: " + ex.Message);
            }
        }
        else
        {
            onDone?.Invoke(null, $"HTTP {resp.statusCode}: {resp.error}");
        }
    }

    private static Dictionary<string, string> BuildAuthHeaders()
    {
        var headers = new Dictionary<string, string>();
        if (JwtManager.Instance != null)
        {
            string token = JwtManager.Instance.GetJwt();
            if (!string.IsNullOrEmpty(token))
            {
                headers["Authorization"] = "Bearer " + token;
            }
        }
        return headers;
    }
}
