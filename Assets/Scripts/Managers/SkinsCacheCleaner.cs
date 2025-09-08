using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Global cleanup hook to remove the cached skins payload on application quit,
/// even if SkinsService was never instantiated in the current session.
/// </summary>
public static class SkinsCacheCleaner
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Init()
    {
        Application.quitting += OnQuitting;
    }

    private static void OnQuitting()
    {
        try
        {
            string dir = Path.Combine(Application.dataPath, "_DebugTokens");
            string cachePath = Path.Combine(dir, "skins_payload.json");
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
                Debug.Log($"SkinsCacheCleaner: Deleted {cachePath}");
            }
            string metaPath = cachePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
                Debug.Log($"SkinsCacheCleaner: Deleted {metaPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SkinsCacheCleaner: Failed to delete cache on quit: {ex.Message}");
        }
        finally
        {
            Application.quitting -= OnQuitting;
        }
    }
}
