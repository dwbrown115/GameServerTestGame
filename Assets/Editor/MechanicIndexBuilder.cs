#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Procederal.Tools
{
    internal static class MechanicIndexBuilder
    {
        private const string ResourcesRoot = "Assets/Resources/ProcederalMechanics";
        private const string MechanicsRoot = "Assets/Scripts/Procederal Item Logic/Mechanics";
        private const string IndexPath = ResourcesRoot + "/index.json";

        [MenuItem("Tools/Procederal Mechanics/Rebuild Index", priority = 100)]
        public static void RebuildIndex()
        {
            var sources = GatherSources();
            if (sources.Count == 0)
            {
                ClearGeneratedCopies();
                WriteIndex(new List<IndexEntry>());
                AssetDatabase.Refresh();
                Debug.LogWarning("[MechanicIndexBuilder] No mechanic JSON files found to index.");
                return;
            }

            SyncGeneratedCopies(sources);
            WriteIndex(ConvertEntries(sources));
            AssetDatabase.Refresh();
            Debug.Log(
                $"[MechanicIndexBuilder] Rebuilt index with {sources.Count} entries at {IndexPath}."
            );
        }

        private static List<SourceEntry> GatherSources()
        {
            var sources = new List<SourceEntry>();
            if (!Directory.Exists(MechanicsRoot))
            {
                Debug.LogWarning($"[MechanicIndexBuilder] Mechanics root missing: {MechanicsRoot}");
                return sources;
            }

            foreach (
                var file in Directory.GetFiles(MechanicsRoot, "*.json", SearchOption.AllDirectories)
            )
            {
                string json = File.ReadAllText(file);
                string mechanicName =
                    ExtractString(json, "MechanicName") ?? Path.GetFileNameWithoutExtension(file);
                string attribute = ExtractString(json, "MechanicAttribute");
                string category = ExtractString(json, "MechanicCategory");
                if (string.IsNullOrEmpty(category))
                    category = string.IsNullOrEmpty(attribute) ? "Primary" : "Modifier";

                string relativeSource = file.Substring(MechanicsRoot.Length + 1).Replace('\\', '/');
                sources.Add(
                    new SourceEntry
                    {
                        mechanicName = mechanicName,
                        attribute = attribute,
                        category = category,
                        json = json,
                        absolutePath = file,
                        sourcePath = relativeSource,
                    }
                );
            }

            return sources;
        }

        private static void SyncGeneratedCopies(List<SourceEntry> sources)
        {
            ClearGeneratedCopies();

            foreach (var src in sources)
            {
                string categoryDirectory = Path.Combine(ResourcesRoot, src.category);
                Directory.CreateDirectory(categoryDirectory);
                string fileName = Path.GetFileName(src.absolutePath);
                string targetFile = Path.Combine(categoryDirectory, fileName);
                File.WriteAllText(targetFile, src.json);

                src.relativeResourceFile = targetFile
                    .Substring(ResourcesRoot.Length + 1)
                    .Replace('\\', '/');
                src.resourcePath = Path.ChangeExtension(src.relativeResourceFile, null);
                src.hash = ComputeSha256(src.absolutePath);
            }
        }

        private static void ClearGeneratedCopies()
        {
            DeleteJsonFiles(Path.Combine(ResourcesRoot, "Primary"));
            DeleteJsonFiles(Path.Combine(ResourcesRoot, "Modifier"));
        }

        private static void DeleteJsonFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
                string meta = file + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
            }
        }

        private static List<IndexEntry> ConvertEntries(List<SourceEntry> sources)
        {
            var entries = new List<IndexEntry>(sources.Count);
            foreach (var src in sources)
            {
                entries.Add(
                    new IndexEntry
                    {
                        mechanicName = src.mechanicName,
                        category = src.category,
                        resourcePath = src.resourcePath,
                        file = src.relativeResourceFile,
                        sourcePath = src.sourcePath,
                        hash = src.hash,
                        attribute = src.attribute,
                    }
                );
            }
            return entries;
        }

        private static string ExtractString(string json, string key)
        {
            var match = Regex.Match(
                json,
                "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.Multiline
            );
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ComputeSha256(string file)
        {
            using var stream = File.OpenRead(file);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        private static void WriteIndex(List<IndexEntry> entries)
        {
            entries.Sort((a, b) => string.CompareOrdinal(a.mechanicName, b.mechanicName));
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                sb.AppendLine("  {");
                var properties = new List<string>
                {
                    $"    \"mechanicName\": \"{Escape(entry.mechanicName)}\"",
                    $"    \"category\": \"{Escape(entry.category)}\"",
                    $"    \"resourcePath\": \"{Escape(entry.resourcePath)}\"",
                    $"    \"file\": \"{Escape(entry.file)}\"",
                    $"    \"sourcePath\": \"{Escape(entry.sourcePath)}\"",
                    $"    \"hash\": \"{entry.hash}\"",
                };
                if (!string.IsNullOrEmpty(entry.attribute))
                    properties.Add($"    \"attribute\": \"{Escape(entry.attribute)}\"");

                for (int j = 0; j < properties.Count; j++)
                {
                    sb.Append(properties[j]);
                    if (j < properties.Count - 1)
                        sb.Append(',');
                    sb.AppendLine();
                }

                sb.Append("  }");
                if (i < entries.Count - 1)
                    sb.Append(',');
                sb.AppendLine();
            }
            sb.Append(']');
            File.WriteAllText(IndexPath, sb.ToString());
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class IndexEntry
        {
            public string mechanicName;
            public string category;
            public string resourcePath;
            public string file;
            public string sourcePath;
            public string hash;
            public string attribute;
        }

        private class SourceEntry
        {
            public string mechanicName;
            public string category;
            public string attribute;
            public string json;
            public string absolutePath;
            public string sourcePath;
            public string relativeResourceFile;
            public string resourcePath;
            public string hash;
        }
    }
}
#endif
