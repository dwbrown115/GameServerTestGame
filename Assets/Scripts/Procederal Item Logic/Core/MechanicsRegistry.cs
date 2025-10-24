using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Procederal.Ota;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Central registry for mechanic descriptors loaded from Primary/Modifier JSON catalogs.
    /// Provides merged settings, incompatibilities, and path/type resolution.
    public class MechanicsRegistry : IMechanicCatalog
    {
        private static MechanicsRegistry _instance;
        public static MechanicsRegistry Instance => _instance ??= new MechanicsRegistry();

        private string _primaryJson;
        private string _modifierJson;
        private bool _initialized;
        private bool _manifestInitialized;
        private MechanicOtaManifest _builtinManifest = MechanicOtaManifest.Empty;
        private MechanicOtaManifest _overlayManifest = MechanicOtaManifest.Empty;
        private MechanicOtaManifest _effectiveManifest = MechanicOtaManifest.Empty;

        // Cache by mechanic name (case-insensitive) for merged settings and path
        private readonly Dictionary<string, Dictionary<string, object>> _mergedCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, string> _pathCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, List<string>> _incompatCache = new(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>
        /// When true, emit verbose diagnostics about manifest resolution and path lookups.
        /// </summary>
        public static bool VerboseLogging { get; set; }

        public void EnsureInitialized(TextAsset primaryJson, TextAsset modifierJson)
        {
            string p = primaryJson != null ? primaryJson.text : null;
            string m = modifierJson != null ? modifierJson.text : null;

            LogVerbose(
                $"EnsureInitialized invoked. primaryJson={(primaryJson != null)}, modifierJson={(modifierJson != null)}"
            );

            if (!_manifestInitialized)
            {
                _builtinManifest = MechanicJsonProjectProvider.LoadManifest();
                _manifestInitialized = true;
                LogVerbose(
                    $"Loaded builtin manifest entries={_builtinManifest?.Entries?.Count ?? 0}"
                );
            }

            RefreshEffectiveManifest();

            if (p == null)
                p = BuildAggregateFromManifest(_effectiveManifest, "Primary");
            if (m == null)
                m = BuildAggregateFromManifest(_effectiveManifest, "Modifier");

            if (_initialized && p == _primaryJson && m == _modifierJson)
            {
                LogVerbose("Initialization skipped (no changes).");
                return;
            }

            _primaryJson = p;
            _modifierJson = m;
            _mergedCache.Clear();
            _pathCache.Clear();
            _incompatCache.Clear();
            _initialized = true;

            FillPaths(_primaryJson);
            FillPaths(_modifierJson);

            LogVerbose(
                $"Initialization complete. Cached primaries={_primaryJson != null}, modifiers={_modifierJson != null}, pathCacheSize={_pathCache.Count}"
            );
        }

        public void ApplyOverlayManifest(MechanicOtaManifest manifest)
        {
            _overlayManifest = manifest ?? MechanicOtaManifest.Empty;
            _initialized = false;
            LogVerbose(
                $"Overlay manifest applied. entries={_overlayManifest?.Entries?.Count ?? 0}"
            );
            EnsureInitialized(null, null);
        }

        public bool TryGetManifestEntry(string mechanicName, out MechanicOtaManifestEntry entry)
        {
            if (_effectiveManifest == null)
            {
                entry = null;
                return false;
            }
            return _effectiveManifest.TryGetEntry(mechanicName, out entry);
        }

        public IReadOnlyList<MechanicOtaManifestEntry> GetManifestEntries()
        {
            return _effectiveManifest?.Entries ?? Array.Empty<MechanicOtaManifestEntry>();
        }

        public bool TryGetPath(string mechanicName, out string path)
        {
            LogVerbose($"TryGetPath called for '{mechanicName}'.");

            path = null;

            if (string.IsNullOrWhiteSpace(mechanicName))
            {
                LogVerbose("Mechanic name missing for TryGetPath.");
                return false;
            }

            if (_pathCache.TryGetValue(mechanicName, out path))
            {
                LogVerbose($"Path cache hit for '{mechanicName}' => {path}");
                return true;
            }

            path = ExtractStringField(_primaryJson, mechanicName, "MechanicPath");
            if (!string.IsNullOrEmpty(path))
            {
                _pathCache[mechanicName] = path;
                LogVerbose($"Path resolved from primary aggregate for '{mechanicName}' => {path}");
                return true;
            }

            path = ExtractStringField(_modifierJson, mechanicName, "MechanicPath");
            if (!string.IsNullOrEmpty(path))
            {
                _pathCache[mechanicName] = path;
                LogVerbose($"Path resolved from modifier aggregate for '{mechanicName}' => {path}");
                return true;
            }

            if (
                _effectiveManifest != null
                && _effectiveManifest.TryGetEntry(mechanicName, out var entry)
            )
            {
                if (MechanicJsonProjectProvider.TryResolveMechanicPath(entry, out var resolvedPath))
                {
                    _pathCache[mechanicName] = resolvedPath;
                    path = resolvedPath;
                    LogVerbose(
                        $"Path resolved from manifest entry for '{mechanicName}' => {resolvedPath}"
                    );
                    return true;
                }

                LogVerboseWarning(
                    $"Manifest entry present but mechanicPath missing for '{mechanicName}'."
                );
            }

            LogVerboseWarning($"Failed to resolve path for '{mechanicName}'.");
            path = null;
            return false;
        }

        public Dictionary<string, object> GetMergedSettings(string mechanicName)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (_mergedCache.TryGetValue(mechanicName, out var cached))
            {
                LogVerbose($"Merged settings cache hit for '{mechanicName}'.");
                return new Dictionary<string, object>(cached, StringComparer.OrdinalIgnoreCase);
            }

            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in GetKvpArray(mechanicName, "Generator"))
                dict[kv.Key] = kv.Value;
            foreach (var kv in GetKvpArray(mechanicName, "Properties"))
                dict[kv.Key] = kv.Value;
            foreach (var kv in GetKvpArray(mechanicName, "Overrides"))
                dict[kv.Key] = kv.Value;
            foreach (var kv in GetKvpArray(mechanicName, "MechanicOverrides"))
                dict[kv.Key] = kv.Value;

            _mergedCache[mechanicName] = new Dictionary<string, object>(
                dict,
                StringComparer.OrdinalIgnoreCase
            );

            LogVerbose($"Cached merged settings for '{mechanicName}' with keys={dict.Count}");

            return dict;
        }

        private void RefreshEffectiveManifest()
        {
            if (ReferenceEquals(_overlayManifest, MechanicOtaManifest.Empty))
            {
                _effectiveManifest = _builtinManifest;
            }
            else if (ReferenceEquals(_builtinManifest, MechanicOtaManifest.Empty))
            {
                _effectiveManifest = _overlayManifest;
            }
            else
            {
                _effectiveManifest = _builtinManifest.Merge(_overlayManifest);
            }

            LogVerbose(
                $"Effective manifest refreshed. entries={_effectiveManifest?.Entries?.Count ?? 0}"
            );
        }

        private string BuildAggregateFromManifest(MechanicOtaManifest manifest, string category)
        {
            if (manifest == null)
            {
                LogVerbose($"BuildAggregate skipped for category={category} (manifest null).");
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            bool wrote = false;

            foreach (var entry in manifest.Entries)
            {
                if (entry == null)
                    continue;
                if (!string.Equals(entry.category, category, StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = LoadJsonFromEntry(entry);
                if (string.IsNullOrWhiteSpace(text))
                {
                    LogVerbose(
                        $"Aggregate skipped empty text for '{entry.mechanicName}' in category={category}"
                    );
                    continue;
                }

                if (wrote)
                    sb.Append(',');
                sb.Append(text.Trim());
                wrote = true;
            }

            sb.Append(']');
            string aggregate = wrote ? sb.ToString() : "[]";
            LogVerbose($"Aggregate built for category={category} length={aggregate.Length}");
            return aggregate;
        }

        private static string LoadJsonFromEntry(MechanicOtaManifestEntry entry)
        {
            if (entry == null)
                return null;

            var projectJson = MechanicJsonProjectProvider.TryLoadMechanicJson(entry);
            if (!string.IsNullOrWhiteSpace(projectJson))
                return projectJson;

            if (!string.IsNullOrWhiteSpace(entry.resourcePath))
            {
                var resPath = $"ProcederalMechanics/{entry.resourcePath}";
                var asset = Resources.Load<TextAsset>(resPath);
                if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                    return asset.text;
            }

            if (!string.IsNullOrWhiteSpace(entry.file))
            {
                string candidate = entry.file;
                if (Path.IsPathRooted(candidate))
                {
                    if (File.Exists(candidate))
                        return File.ReadAllText(candidate);
                }
                else
                {
                    string normalized = candidate.Replace('/', Path.DirectorySeparatorChar);
                    foreach (var root in GetRuntimeSearchRoots())
                    {
                        if (string.IsNullOrWhiteSpace(root))
                            continue;

                        var direct = Path.Combine(root, normalized);
                        if (File.Exists(direct))
                            return File.ReadAllText(direct);

                        var mechanics = Path.Combine(root, "Mechanics", normalized);
                        if (File.Exists(mechanics))
                            return File.ReadAllText(mechanics);

                        var procederal = Path.Combine(root, "ProcederalMechanics", normalized);
                        if (File.Exists(procederal))
                            return File.ReadAllText(procederal);
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> GetRuntimeSearchRoots()
        {
            if (!string.IsNullOrWhiteSpace(Application.persistentDataPath))
                yield return Application.persistentDataPath;

            var streaming = Application.streamingAssetsPath;
            if (
                !string.IsNullOrWhiteSpace(streaming)
                && !streaming.StartsWith("jar", StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(streaming)
            )
                yield return streaming;

            var dataPath = Application.dataPath;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                var parent = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    yield return parent;
            }
        }

        public Dictionary<string, object> GetKvpArray(string mechanicName, string arrayName)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_primaryJson))
                TryExtractKvpArray(_primaryJson, mechanicName, arrayName, result);
            if (!string.IsNullOrWhiteSpace(_modifierJson) && result.Count == 0)
                TryExtractKvpArray(_modifierJson, mechanicName, arrayName, result);
            return result;
        }

        public List<string> GetIncompatibleWith(string mechanicName)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return new List<string>();
            if (_incompatCache.TryGetValue(mechanicName, out var cached))
                return new List<string>(cached);
            var list = new List<string>();
            ExtractStringArray(_primaryJson, mechanicName, "IncompatibleWith", list);
            _incompatCache[mechanicName] = new List<string>(list);
            return list;
        }

        private void FillPaths(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    break;
                int colon = json.IndexOf(':', nameKey);
                int q1 = json.IndexOf('"', colon + 1);
                int q2 = json.IndexOf('"', q1 + 1);
                if (colon < 0 || q1 < 0 || q2 < 0)
                    break;
                string foundName = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                string path = ExtractStringFieldAfter(json, searchPos, "MechanicPath");
                if (!string.IsNullOrWhiteSpace(foundName) && !string.IsNullOrWhiteSpace(path))
                {
                    _pathCache[foundName] = path;
                    LogVerbose($"FillPaths cached path for '{foundName}' => {path}");
                }
            }
        }

        private static string ExtractStringField(string json, string mechanicName, string field)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(mechanicName))
                return null;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    return null;
                int colon = json.IndexOf(':', nameKey);
                int q1 = json.IndexOf('"', colon + 1);
                int q2 = json.IndexOf('"', q1 + 1);
                if (colon < 0 || q1 < 0 || q2 < 0)
                    return null;
                string found = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                if (!string.Equals(found, mechanicName, comp))
                    continue;
                return ExtractStringFieldAfter(json, searchPos, field);
            }
        }

        private static string ExtractStringFieldAfter(string json, int startPos, string field)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            var comp = StringComparison.OrdinalIgnoreCase;
            int fieldKey = json.IndexOf("\"" + field + "\"", startPos, comp);
            if (fieldKey < 0)
                return null;
            int colon = json.IndexOf(':', fieldKey);
            if (colon < 0)
                return null;
            int q1 = json.IndexOf('"', colon + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0)
                return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static void ExtractStringArray(
            string json,
            string mechanicName,
            string arrayName,
            List<string> output
        )
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(mechanicName))
                return;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    return;
                int colon = json.IndexOf(':', nameKey);
                int q1 = json.IndexOf('"', colon + 1);
                int q2 = json.IndexOf('"', q1 + 1);
                if (colon < 0 || q1 < 0 || q2 < 0)
                    return;
                string found = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                if (!string.Equals(found, mechanicName, comp))
                    continue;
                int arrKey = json.IndexOf("\"" + arrayName + "\"", searchPos, comp);
                if (arrKey < 0)
                    return;
                int arrColon = json.IndexOf(':', arrKey);
                int open = json.IndexOf('[', arrColon);
                if (arrColon < 0 || open < 0)
                    return;
                int depth = 0;
                int i = open;
                for (; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '[')
                        depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                            break;
                    }
                }
                if (i >= json.Length)
                    return;
                string inner = json.Substring(open + 1, i - open - 1);
                int p = 0;
                while (true)
                {
                    int s1 = inner.IndexOf('"', p);
                    if (s1 < 0)
                        break;
                    int s2 = inner.IndexOf('"', s1 + 1);
                    if (s2 < 0)
                        break;
                    string val = inner.Substring(s1 + 1, s2 - s1 - 1);
                    if (!string.IsNullOrWhiteSpace(val))
                        output.Add(val);
                    p = s2 + 1;
                }
                return;
            }
        }

        private static bool TryExtractKvpArray(
            string json,
            string mechanicName,
            string arrayName,
            Dictionary<string, object> output
        )
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(mechanicName))
                return false;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    return false;
                int colon = json.IndexOf(':', nameKey);
                if (colon < 0)
                    return false;
                int q1 = json.IndexOf('"', colon + 1);
                if (q1 < 0)
                    return false;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0)
                    return false;
                string found = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                if (!string.Equals(found, mechanicName, comp))
                    continue;

                int arrKey = json.IndexOf("\"" + arrayName + "\"", searchPos, comp);
                if (arrKey < 0)
                    return false;
                int arrColon = json.IndexOf(':', arrKey);
                if (arrColon < 0)
                    return false;
                int open = json.IndexOf('[', arrColon);
                if (open < 0)
                    return false;
                int depth = 0;
                int i = open;
                for (; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '[')
                        depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                            break;
                    }
                }
                if (i >= json.Length)
                    return false;
                string inner = json.Substring(open + 1, i - open - 1);

                int p = 0;
                while (true)
                {
                    int ob = inner.IndexOf('{', p);
                    if (ob < 0)
                        break;
                    int cb = inner.IndexOf('}', ob + 1);
                    if (cb < 0)
                        break;
                    string obj = inner.Substring(ob + 1, cb - ob - 1);
                    int kq1 = obj.IndexOf('"');
                    if (kq1 >= 0)
                    {
                        int kq2 = obj.IndexOf('"', kq1 + 1);
                        if (kq2 > kq1)
                        {
                            string key = obj.Substring(kq1 + 1, kq2 - kq1 - 1);
                            int vcolon = obj.IndexOf(':', kq2 + 1);
                            if (vcolon > 0)
                            {
                                string raw = obj.Substring(vcolon + 1).Trim();
                                object parsed = ParseValue(raw);
                                if (!string.IsNullOrWhiteSpace(key))
                                    output[key] = parsed;
                            }
                        }
                    }
                    p = cb + 1;
                }
                return output.Count > 0;
            }
        }

        private static object ParseValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            raw = raw.Trim();
            if (raw.StartsWith("[") && raw.EndsWith("]"))
            {
                var inner = raw.Substring(1, raw.Length - 2).Trim();
                if (inner.Length == 0)
                    return Array.Empty<string>();
                var list = new List<string>();
                int i = 0;
                while (i < inner.Length)
                {
                    while (i < inner.Length && char.IsWhiteSpace(inner[i]))
                        i++;
                    if (i >= inner.Length)
                        break;
                    if (inner[i] == '"')
                    {
                        int j = inner.IndexOf('"', i + 1);
                        if (j < 0)
                            break;
                        string token = inner.Substring(i + 1, j - i - 1);
                        list.Add(token);
                        i = j + 1;
                    }
                    else
                    {
                        int j = inner.IndexOf(',', i);
                        string token = (
                            j < 0 ? inner.Substring(i) : inner.Substring(i, j - i)
                        ).Trim();
                        if (token.Length > 0)
                            list.Add(token);
                        i = j < 0 ? inner.Length : j + 1;
                    }
                    while (i < inner.Length && inner[i] != '"' && inner[i] != ',')
                        i++;
                    if (i < inner.Length && inner[i] == ',')
                        i++;
                }
                return list.ToArray();
            }
            if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
                return raw.Substring(1, raw.Length - 2);
            if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            if (
                float.TryParse(
                    raw,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var f
                )
            )
            {
                if (Mathf.Approximately(f, Mathf.Round(f)))
                    return (int)Mathf.Round(f);
                return f;
            }
            return raw;
        }

        private static void LogVerbose(string message)
        {
            if (VerboseLogging)
                Debug.Log("[MechanicsRegistry] " + message);
        }

        private static void LogVerboseWarning(string message)
        {
            if (VerboseLogging)
                Debug.LogWarning("[MechanicsRegistry] " + message);
        }
    }
}
