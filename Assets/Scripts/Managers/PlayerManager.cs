using System;
using System.IO;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    private string playerDataPath;
    private string saltPath; // Shared with JwtManager

    private PlayerResponse currentPlayer;

    // The derived key is cached for performance. Key derivation is expensive.
    private byte[] _derivedKey;

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

        playerDataPath = Path.Combine(debugFolder, "player_data.dat");
        // Both managers will use the same salt file.
        saltPath = Path.Combine(debugFolder, "jwt_salt.dat");

        // Key derivation is expensive, so do it once and cache it.
        try
        {
            byte[] salt = CryptoUtils.GetOrCreateSalt(saltPath);
            _derivedKey = CryptoUtils.DeriveKey(DeviceUtils.GetDeviceId(), salt);
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to derive encryption key: {ex.Message}");
            // Without a key, we can't save/load. This is a critical failure.
        }

        LoadPlayerFromDisk();
    }

    public void SetPlayerData(string id, string name)
    {
        if (currentPlayer == null)
            currentPlayer = new PlayerResponse();

        // Only update the ID if a new, non-null/empty ID is provided.
        if (!string.IsNullOrEmpty(id))
            currentPlayer.userId = id;

        // Only update the name if a new, non-null name is provided.
        // This prevents accidentally overwriting a known name with null.
        if (name != null)
            currentPlayer.userName = name;

        Debug.Log($"Player data set: ID={currentPlayer.userId}, Name={currentPlayer.userName}");

        // Automatically save when data is set.
        SavePlayerToDisk();
    }

    public PlayerResponse GetPlayerData() => currentPlayer;

    public string GetUserId() => currentPlayer?.userId;

    public string GetPlayerName() => currentPlayer?.userName;

    public void ClearPlayerData()
    {
        currentPlayer = null;
        if (File.Exists(playerDataPath))
        {
            File.Delete(playerDataPath);
            Debug.Log("🗑️ Cleared player data from disk.");

            // Also delete the .meta file Unity generates for it in the editor.
            string metaPath = playerDataPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
                Debug.Log("🗑️ Cleared player data .meta file from disk.");
            }
        }
    }

    public void SavePlayerToDisk()
    {
        if (currentPlayer == null || string.IsNullOrEmpty(currentPlayer.userId))
        {
            Debug.LogWarning("⚠️ Player data is null or User ID is empty. Skipping save.");
            return;
        }

        if (_derivedKey == null)
        {
            Debug.LogError("❌ Cannot save player data: encryption key is not available.");
            return;
        }

        // Using JsonUtility to serialize the PlayerData object.
        string playerDataJson = JsonUtility.ToJson(currentPlayer);
        Debug.Log($"📝 Raw player data (JSON): {playerDataJson}");

        try
        {
            byte[] encryptedData = CryptoUtils.EncryptAES(playerDataJson, _derivedKey);

            File.WriteAllBytes(playerDataPath, encryptedData);
            Debug.Log($"💾 Saved encrypted player data (size: {encryptedData.Length} bytes).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to save player data: {ex.Message}");
        }
    }

    private void LoadPlayerFromDisk()
    {
        if (!File.Exists(playerDataPath))
        {
            Debug.Log("📁 Player data file not found. No player data loaded.");
            return;
        }

        if (_derivedKey == null)
        {
            Debug.LogError("❌ Cannot load player data: encryption key is not available.");
            return;
        }

        try
        {
            byte[] encryptedData = File.ReadAllBytes(playerDataPath);
            if (encryptedData.Length == 0)
            {
                Debug.LogWarning("⚠️ Player data file is empty.");
                return;
            }

            string decryptedJson = CryptoUtils.DecryptAES(encryptedData, _derivedKey);

            if (string.IsNullOrEmpty(decryptedJson))
            {
                Debug.LogWarning("⚠️ Decrypted player data is empty. Clearing data.");
                ClearPlayerData();
                return;
            }

            currentPlayer = JsonUtility.FromJson<PlayerResponse>(decryptedJson);
            Debug.Log(
                $"✅ Loaded player data: ID={currentPlayer.userId}, Name={currentPlayer.userName}"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"❌ Failed to load and decrypt player data: {ex.Message}. Deleting corrupt file."
            );
            // If decryption fails, the file is likely corrupt or the key changed.
            // Delete it to prevent repeated errors.
            ClearPlayerData();
        }
    }
}
