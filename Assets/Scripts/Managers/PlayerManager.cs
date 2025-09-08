using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    private string playerDataPath;
    private string playerDataDebugCopyPath;
    private string saltPath; // Shared with JwtManager

    private PlayerResponse currentPlayer;

    [Header("Debug Persistence")]
    [Tooltip(
        "If true, saves player_data.dat as plaintext JSON instead of encrypted (temporary debugging only!)."
    )]
    [SerializeField]
    private bool savePlaintext = false;

    [Tooltip(
        "If true, always write a plaintext JSON debug copy next to the encrypted file (player_data.debug.json)."
    )]
    [SerializeField]
    private bool writeDebugCopy = false;

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
        playerDataDebugCopyPath = Path.Combine(debugFolder, "player_data.debug.json");
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

        // Persist userId and any cosmetic data (skin/color). The userName is session data.
        // Attempt to preserve existing persisted cosmetic values if present.
        var existing = LoadPersistentUnsafe();
        var persistentData = new PersistentPlayerData
        {
            userId = currentPlayer.userId,
            skinId = existing?.skinId,
            hexValue = existing?.hexValue,
            points = existing?.points ?? 0,
            ownedSkinIds = existing?.ownedSkinIds,
        };
        string playerDataJson = JsonConvert.SerializeObject(persistentData);
        Debug.Log($"📝 Persisting player data (JSON): {playerDataJson}");

        try
        {
            if (savePlaintext)
            {
                File.WriteAllText(playerDataPath, playerDataJson);
                Debug.Log("💾 Saved plaintext player data (debug mode).");
            }
            else
            {
                byte[] encryptedData = CryptoUtils.EncryptAES(playerDataJson, _derivedKey);
                File.WriteAllBytes(playerDataPath, encryptedData);
                Debug.Log($"💾 Saved encrypted player data (size: {encryptedData.Length} bytes).");
            }

            if (writeDebugCopy)
            {
                File.WriteAllText(playerDataDebugCopyPath, playerDataJson);
                Debug.Log($"🧪 Wrote debug copy: {playerDataDebugCopyPath}");
            }
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
            // Read file and detect plaintext vs encrypted
            byte[] rawBytes = File.ReadAllBytes(playerDataPath);
            if (rawBytes.Length == 0)
            {
                Debug.LogWarning("⚠️ Player data file is empty.");
                return;
            }

            string jsonText = null;
            // If debug savePlaintext is on, treat as plaintext
            if (savePlaintext)
            {
                jsonText = File.ReadAllText(playerDataPath);
            }
            else
            {
                // Heuristic: if file starts with '{' it's likely plaintext JSON even if flag is off
                if (rawBytes[0] == (byte)'{')
                {
                    jsonText = File.ReadAllText(playerDataPath);
                    Debug.LogWarning(
                        "ℹ️ Detected plaintext player_data.dat while not in debug mode; loading as JSON."
                    );
                }
                else
                {
                    jsonText = CryptoUtils.DecryptAES(rawBytes, _derivedKey);
                }
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogWarning(
                    "⚠️ Player data JSON is empty after read/decrypt. Clearing data file."
                );
                ClearPlayerData();
                return;
            }

            var persistentData = JsonConvert.DeserializeObject<PersistentPlayerData>(jsonText);

            if (currentPlayer == null)
                currentPlayer = new PlayerResponse();

            currentPlayer.userId = persistentData.userId;
            currentPlayer.userName = null; // This is session data, will be fetched after validation.
            Debug.Log(
                $"✅ Loaded persistent player data: ID={currentPlayer.userId} SkinId={persistentData.skinId} Hex={persistentData.hexValue} Points={persistentData.points} OwnedSkins={(persistentData.ownedSkinIds != null ? string.Join(",", persistentData.ownedSkinIds) : "<none>")}"
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

    // Helper to read existing persisted data without mutating currentPlayer. Returns null on failure.
    private PersistentPlayerData LoadPersistentUnsafe()
    {
        try
        {
            if (!File.Exists(playerDataPath) || _derivedKey == null)
                return null;
            byte[] encryptedData = File.ReadAllBytes(playerDataPath);
            if (encryptedData == null || encryptedData.Length == 0)
                return null;
            // Try plaintext if debug is enabled or file looks like JSON; else decrypt
            if (savePlaintext || (encryptedData.Length > 0 && encryptedData[0] == (byte)'{'))
            {
                string jsonPT = File.ReadAllText(playerDataPath);
                if (string.IsNullOrEmpty(jsonPT))
                    return null;
                return JsonConvert.DeserializeObject<PersistentPlayerData>(jsonPT);
            }
            else
            {
                string json = CryptoUtils.DecryptAES(encryptedData, _derivedKey);
                if (string.IsNullOrEmpty(json))
                    return null;
                return JsonConvert.DeserializeObject<PersistentPlayerData>(json);
            }
        }
        catch
        {
            return null;
        }
    }

    public void SetActiveSkin(string skinId, string hexValue)
    {
        var existing = LoadPersistentUnsafe() ?? new PersistentPlayerData();
        existing.userId = GetUserId();
        existing.skinId = skinId;
        existing.hexValue = hexValue;
        Persist(existing);
    }

    public void SetPoints(int points)
    {
        var existing = LoadPersistentUnsafe() ?? new PersistentPlayerData();
        existing.userId = GetUserId();
        existing.points = Mathf.Max(0, points);
        Persist(existing);
    }

    public int GetPoints()
    {
        var p = LoadPersistentUnsafe();
        return p?.points ?? 0;
    }

    public void SetOwnedSkins(string[] owned)
    {
        var existing = LoadPersistentUnsafe() ?? new PersistentPlayerData();
        existing.userId = GetUserId();
        existing.ownedSkinIds = owned;
        Persist(existing);
    }

    public string[] GetOwnedSkins()
    {
        var p = LoadPersistentUnsafe();
        return p?.ownedSkinIds;
    }

    private void Persist(PersistentPlayerData data)
    {
        string json = JsonConvert.SerializeObject(data);
        try
        {
            if (savePlaintext)
            {
                File.WriteAllText(playerDataPath, json);
                Debug.Log("💾 Saved player_data.dat (plaintext debug mode).");
            }
            else
            {
                if (_derivedKey == null)
                {
                    Debug.LogError("❌ Cannot persist player data: encryption key not available.");
                    return;
                }
                byte[] encrypted = CryptoUtils.EncryptAES(json, _derivedKey);
                File.WriteAllBytes(playerDataPath, encrypted);
                Debug.Log("💾 Saved player_data.dat (encrypted).");
            }

            if (writeDebugCopy)
            {
                File.WriteAllText(playerDataDebugCopyPath, json);
                Debug.Log($"🧪 Wrote debug copy: {playerDataDebugCopyPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Failed to persist player data: {ex.Message}");
        }
    }

    public string GetSavedSkinId()
    {
        var p = LoadPersistentUnsafe();
        return p?.skinId;
    }

    public string GetSavedSkinHex()
    {
        var p = LoadPersistentUnsafe();
        return p?.hexValue;
    }
}
