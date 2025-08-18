using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
internal class TokenData
{
    public string JwtToken;
    public string RefreshToken;
    public DateTime ExpiresAt;
}

[Serializable]
public class TokenValidationRequest
{
    public string UserId;
    public string Token;
    public string RefreshToken;
    public string DeviceId;
}

public class JwtManager : MonoBehaviour
{
    public static event Action<bool> OnAuthStateChanged;

    public static JwtManager Instance;

    private string tokenPath;
    private string saltPath;

    private string jwtToken;
    private string refreshToken;
    private DateTime expiresAt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        string debugFolder = Path.Combine(Application.dataPath, "_DebugTokens");
        if (!Directory.Exists(debugFolder))
            Directory.CreateDirectory(debugFolder);

        tokenPath = Path.Combine(debugFolder, "jwt_token.dat");
        saltPath = Path.Combine(debugFolder, "jwt_salt.dat");

        LoadTokenFromDisk();
    }

    public void SetToken(string jwt, string refreshToken, string userId, DateTime? expiresAtDateTime)
    {
        DateTime expiry = expiresAtDateTime ?? DateTime.MinValue; // Use provided DateTime or MinValue if null

        if (string.IsNullOrEmpty(jwt) || string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("🚫 Attempted to save empty token or refresh token.");
            Debug.Log(
                $"🛎 SetToken → Token: {jwt}, Refresh: {refreshToken}, ExpiresAt: {expiresAtDateTime}"
            );
            return;
        }

        jwtToken = jwt;
        Debug.Log($"✅ JWT set: {jwtToken}");
        this.refreshToken = refreshToken;
        Debug.Log($"✅ Refresh Token set: {this.refreshToken}");
        expiresAt = expiry;
        Debug.Log($"✅ Token expires at: {expiresAt}");

        // The userId is set here. The userName will be fetched from the server separately.
        // We pass null for the name to ensure any old session data is cleared.
        PlayerManager.Instance.SetPlayerData(userId, null);

        SaveTokenToDisk();

        OnAuthStateChanged?.Invoke(true);
    }

    public string GetJwt()
    {
        Debug.Log($"JwtManager: GetJwt() returning: {jwtToken}");
        return jwtToken;
    }

    public string GetRefreshToken()
    {
        Debug.Log($"JwtManager: GetRefreshToken() returning: {refreshToken}");
        return refreshToken;
    }

    public static DateTime? ParseJwtExpiry(string jwtToken)
    {
        if (string.IsNullOrEmpty(jwtToken))
        {
            Debug.LogWarning("Attempted to parse expiry from an empty JWT.");
            return null;
        }

        try
        {
            // JWTs have three parts: Header.Payload.Signature
            string[] parts = jwtToken.Split('.');
            if (parts.Length < 2)
            {
                Debug.LogWarning("Invalid JWT format: Not enough parts.");
                return null;
            }

            // Decode the payload (base64url-encoded)
            string payloadBase64 = parts[1];
            // Replace base64url characters with base64 characters
            payloadBase64 = payloadBase64.Replace('-', '+').Replace('_', '/');
            // Pad with '=' characters if necessary
            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            byte[] decodedBytes = Convert.FromBase64String(payloadBase64);
            string decodedPayload = Encoding.UTF8.GetString(decodedBytes);

            // Deserialize the JSON payload
            var payload = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, object>>(decodedPayload);

            if (payload != null && payload.ContainsKey("exp"))
            {
                long expUnixTimestamp = Convert.ToInt64(payload["exp"]);
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(expUnixTimestamp);
                return dateTimeOffset.UtcDateTime; // Return DateTime directly
            }
            else
            {
                Debug.LogWarning("JWT payload does not contain 'exp' claim.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse JWT expiry: {ex.Message}");
            return null;
        }
    }

    public DateTime GetExpiry() => expiresAt;

    public bool IsTokenValid()
    {
        bool isValid = !string.IsNullOrEmpty(jwtToken) && DateTime.UtcNow < expiresAt;
        // Debug.Log(
        //     $"JwtManager: IsTokenValid() returning: {isValid} (CurrentTime: {DateTime.UtcNow}, ExpiresAt: {expiresAt})"
        // );
        return isValid;
    }

    public void ClearToken()
    {
        Debug.Log("JwtManager: Clearing token data.");
        jwtToken = null;
        refreshToken = null;
        expiresAt = DateTime.MinValue;
        if (File.Exists(tokenPath))
            File.Delete(tokenPath);

        // Player data is now managed by PlayerManager.
        PlayerManager.Instance.ClearPlayerData();

        OnAuthStateChanged?.Invoke(false);
    }

    private void SaveTokenToDisk()
    {
        if (string.IsNullOrEmpty(jwtToken) || string.IsNullOrEmpty(refreshToken))
        {
            Debug.LogWarning("⚠️ Token data is incomplete. Skipping save.");
            return;
        }

        var tokenData = new TokenData
        {
            JwtToken = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
        };

        string tokenJson = JsonConvert.SerializeObject(tokenData);
        Debug.Log($"📝 Raw token data (JSON): {tokenJson}");

        try
        {
            byte[] salt = CryptoUtils.GetOrCreateSalt(saltPath);
            byte[] key = CryptoUtils.DeriveKey(DeviceUtils.GetDeviceId(), salt);
            byte[] encrypted = CryptoUtils.EncryptAES(tokenJson, key);
            File.WriteAllBytes(tokenPath, encrypted);
            Debug.Log($"💾 Saved encrypted token");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to save token: {ex.Message}");
        }
    }

    private void LoadTokenFromDisk()
    {
        if (!File.Exists(tokenPath) || !File.Exists(saltPath))
        {
            Debug.Log("📁 Token or salt file missing. Skipping load.");
            return;
        }

        try
        {
            byte[] encryptedData = File.ReadAllBytes(tokenPath);
            if (encryptedData == null || encryptedData.Length == 0)
            {
                Debug.LogWarning("⚠️ Token file is empty.");
                return;
            }

            byte[] salt = File.ReadAllBytes(saltPath);
            byte[] key = CryptoUtils.DeriveKey(DeviceUtils.GetDeviceId(), salt);
            string decryptedJson = CryptoUtils.DecryptAES(encryptedData, key);

            if (string.IsNullOrEmpty(decryptedJson))
            {
                Debug.LogWarning("⚠️ Decrypted token data is empty. Clearing token.");
                ClearToken();
                return;
            }

            var tokenData = JsonConvert.DeserializeObject<TokenData>(decryptedJson);
            jwtToken = tokenData.JwtToken;
            refreshToken = tokenData.RefreshToken;
            expiresAt = tokenData.ExpiresAt; // Load DateTime directly

            Debug.Log($"✅ Loaded JWT: {jwtToken}");
            Debug.Log($"✅ Loaded Refresh Token: {refreshToken}");
            Debug.Log($"⏳ Token expires at: {expiresAt}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to load token: {ex.Message}");
            // If decryption fails, the file is likely corrupt or the key changed.
            // Delete it to prevent repeated errors.
            // ClearToken();
        }
    }
}
