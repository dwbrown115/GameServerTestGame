using System;
using System.IO;
using Game.Procederal.Ota;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// <summary>
    /// Centralizes access to mechanic JSON data stored alongside the mechanic scripts.
    /// Provides simple helpers so runtime systems do not need to be aware of the on-disk layout.
    /// </summary>
    public static class MechanicJsonProjectProvider
    {
        private static readonly string[] MechanicsRelativePath =
        {
            "Scripts",
            "Procederal Item Logic",
            "Mechanics",
        };

        /// <summary>
        /// Resolves the absolute path to the mechanics JSON root within the project.
        /// Returns null when the path cannot be determined (for example, outside of Unity runtime).
        /// </summary>
        private static string GetMechanicsRoot()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
                return null;

            string root = dataPath;
            for (int i = 0; i < MechanicsRelativePath.Length; i++)
                root = Path.Combine(root, MechanicsRelativePath[i]);
            return root;
        }

        /// <summary>
        /// Loads the mechanic catalog manifest from the mechanics folder when available, falling back to
        /// the legacy Resources manifest for compatibility.
        /// </summary>
        public static MechanicOtaManifest LoadManifest()
        {
            string root = GetMechanicsRoot();
            if (!string.IsNullOrWhiteSpace(root))
            {
                string indexPath = Path.Combine(root, "index.json");
                if (File.Exists(indexPath))
                {
                    try
                    {
                        string text = File.ReadAllText(indexPath);
                        if (!string.IsNullOrWhiteSpace(text))
                            return MechanicOtaManifest.FromJson(text, indexPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[MechanicJsonProjectProvider] Failed to read mechanics index '{indexPath}': {ex.Message}"
                        );
                    }
                }
            }

            // Fallback ensures older resource-based setups continue to function.
            return MechanicOtaManifest.LoadFromResources();
        }

        /// <summary>
        /// Attempts to read the raw JSON payload for a mechanic from the project mechanics folder.
        /// Returns null when the JSON is not present or cannot be read.
        /// </summary>
        public static string TryLoadMechanicJson(Game.Procederal.Ota.MechanicOtaManifestEntry entry)
        {
            if (entry == null)
                return null;

            string root = GetMechanicsRoot();
            if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(entry.sourcePath))
            {
                string relative = entry.sourcePath.Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.Combine(root, relative);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        return File.ReadAllText(fullPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[MechanicJsonProjectProvider] Failed to read mechanic json '{fullPath}': {ex.Message}"
                        );
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(entry.file))
            {
                string normalized = entry.file.Replace('/', Path.DirectorySeparatorChar);
                string fallback = Path.Combine(root, normalized);
                if (File.Exists(fallback))
                {
                    try
                    {
                        return File.ReadAllText(fallback);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[MechanicJsonProjectProvider] Failed to read mechanic fallback '{fallback}': {ex.Message}"
                        );
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve the mechanic path for the provided manifest entry by inspecting the JSON file.
        /// </summary>
        public static bool TryResolveMechanicPath(
            Game.Procederal.Ota.MechanicOtaManifestEntry entry,
            out string mechanicPath
        )
        {
            mechanicPath = null;
            var json = TryLoadMechanicJson(entry);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var header = JsonUtility.FromJson<MechanicJsonHeader>(json);
                if (header != null)
                {
                    if (!string.IsNullOrWhiteSpace(header.MechanicPath))
                    {
                        mechanicPath = header.MechanicPath.Trim();
                        return true;
                    }
                    if (!string.IsNullOrWhiteSpace(header.mechanicPath))
                    {
                        mechanicPath = header.mechanicPath.Trim();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[MechanicJsonProjectProvider] Failed to parse mechanic json for path: {ex.Message}"
                );
            }

            string extracted = ExtractStringField(json, "MechanicPath");
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                mechanicPath = extracted.Trim();
                return true;
            }
            extracted = ExtractStringField(json, "mechanicPath");
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                mechanicPath = extracted.Trim();
                return true;
            }

            return false;
        }

        private static string ExtractStringField(string json, string field)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(field))
                return null;

            var comp = StringComparison.OrdinalIgnoreCase;
            int key = json.IndexOf("\"" + field + "\"", comp);
            if (key < 0)
                return null;
            int colon = json.IndexOf(':', key);
            if (colon < 0)
                return null;
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0)
                return null;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0)
                return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        [Serializable]
        private sealed class MechanicJsonHeader
        {
            public string MechanicPath;
            public string mechanicPath;
        }
    }
}
