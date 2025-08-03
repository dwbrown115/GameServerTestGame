using System;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
internal class TokenData
{
    public string JwtToken;
    public string RefreshToken;
    public string ExpiresAt;
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

    public void SetToken(LoginResult result)
    {
        if (
            !DateTime.TryParse(
                result.expiresAt,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime expiry
            )
        )
        {
            Debug.LogWarning("⚠️ Failed to parse expiry from LoginResult. Defaulting to MinValue.");
            expiry = DateTime.MinValue;
        }

        if (string.IsNullOrEmpty(result?.token) || string.IsNullOrEmpty(result?.refreshToken))
        {
            Debug.LogWarning("🚫 Attempted to save empty token or refresh token.");
            Debug.Log(
                $"🛎 LoginResult → Token: {result?.token}, Refresh: {result?.refreshToken}, ExpiresAt: {result?.expiresAt}"
            );
            return;
        }

        jwtToken = result.token;
        Debug.Log($"✅ JWT set: {jwtToken}");
        refreshToken = result.refreshToken;
        Debug.Log($"✅ Refresh Token set: {refreshToken}");
        expiresAt = expiry;
        Debug.Log($"✅ Token expires at: {expiresAt}");

        // The userId is set here. The userName will be fetched from the server separately.
        // We pass null for the name to ensure any old session data is cleared.
        PlayerManager.Instance.SetPlayerData(result.userId, null);

        SaveTokenToDisk();

        OnAuthStateChanged?.Invoke(true);
    }

    public string GetJwt() => jwtToken;

    public string GetRefreshToken() => refreshToken;

    public DateTime GetExpiry() => expiresAt;

    public bool IsTokenValid() => !string.IsNullOrEmpty(jwtToken) && DateTime.UtcNow < expiresAt;

    public void ClearToken()
    {
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
            ExpiresAt = expiresAt.ToString("o"), // ISO 8601 format
        };

        string tokenJson = JsonUtility.ToJson(tokenData);
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

            var tokenData = JsonUtility.FromJson<TokenData>(decryptedJson);
            jwtToken = tokenData.JwtToken;
            refreshToken = tokenData.RefreshToken;
            if (
                !DateTime.TryParse(
                    tokenData.ExpiresAt,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out expiresAt
                )
            )
            {
                Debug.LogWarning("⚠️ Failed to parse token expiry date. Setting to MinValue.");
                expiresAt = DateTime.MinValue;
            }

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
