using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Central registry for mechanic descriptors loaded from Primary/Modifier JSON catalogs.
    /// Provides merged settings, incompatibilities, and path/type resolution.
    public class MechanicsRegistry
    {
        private static MechanicsRegistry _instance;
        public static MechanicsRegistry Instance => _instance ??= new MechanicsRegistry();

        private string _primaryJson;
        private string _modifierJson;
        private bool _initialized;

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

        public void EnsureInitialized(TextAsset primaryJson, TextAsset modifierJson)
        {
            string p = primaryJson != null ? primaryJson.text : (_primaryJson ?? string.Empty);
            string m = modifierJson != null ? modifierJson.text : (_modifierJson ?? string.Empty);
            if (_initialized && p == _primaryJson && m == _modifierJson)
                return;
            _primaryJson = p;
            _modifierJson = m;
            _mergedCache.Clear();
            _pathCache.Clear();
            _incompatCache.Clear();
            _initialized = true;

            // Pre-scan for paths for quick lookups
            FillPaths(_primaryJson);
            FillPaths(_modifierJson);
        }

        public bool TryGetPath(string mechanicName, out string path)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
            {
                path = null;
                return false;
            }
            if (_pathCache.TryGetValue(mechanicName, out path))
                return true;
            // Fallback: attempt parse once
            path = ExtractStringField(_primaryJson, mechanicName, "MechanicPath");
            if (!string.IsNullOrEmpty(path))
            {
                _pathCache[mechanicName] = path;
                return true;
            }
            path = ExtractStringField(_modifierJson, mechanicName, "MechanicPath");
            if (!string.IsNullOrEmpty(path))
            {
                _pathCache[mechanicName] = path;
                return true;
            }
            return false;
        }

        public Dictionary<string, object> GetMergedSettings(string mechanicName)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (_mergedCache.TryGetValue(mechanicName, out var cached))
                return new Dictionary<string, object>(cached, StringComparer.OrdinalIgnoreCase);

            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
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
            return dict;
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
                    _pathCache[foundName] = path;
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
            // Simple string array parsing: ["A","B"] -> string[] {A,B}
            if (raw.StartsWith("[") && raw.EndsWith("]"))
            {
                // Attempt lightweight parse for string tokens only
                var inner = raw.Substring(1, raw.Length - 2).Trim();
                if (inner.Length == 0)
                    return new string[0];
                var list = new System.Collections.Generic.List<string>();
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
                        // Non-quoted token until comma
                        int j = inner.IndexOf(',', i);
                        string token = (
                            j < 0 ? inner.Substring(i) : inner.Substring(i, j - i)
                        ).Trim();
                        if (token.Length > 0)
                            list.Add(token);
                        i = (j < 0 ? inner.Length : j + 1);
                    }
                    // Skip comma
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
                // Preserve ints when possible
                if (Mathf.Approximately(f, Mathf.Round(f)))
                    return (int)Mathf.Round(f);
                return f;
            }
            return raw; // fallback string
        }
    }
}
