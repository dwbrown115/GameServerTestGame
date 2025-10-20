using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Centralizes common value normalization for mechanic settings loaded from loosely typed JSON blobs.
    public static class MechanicSettingNormalizer
    {
        public static float Float(
            IDictionary<string, object> source,
            string key,
            float fallback,
            float min = float.NegativeInfinity,
            float max = float.PositiveInfinity
        )
        {
            if (TryGet(source, key, out float value))
                return Mathf.Clamp(value, min, max);
            return Mathf.Clamp(fallback, min, max);
        }

        public static int Int(
            IDictionary<string, object> source,
            string key,
            int fallback,
            int min = int.MinValue,
            int max = int.MaxValue
        )
        {
            if (TryGet(source, key, out int value))
                return Mathf.Clamp(value, min, max);
            return Mathf.Clamp(fallback, min, max);
        }

        public static int Int(
            IDictionary<string, object> source,
            int fallback,
            params string[] keys
        )
        {
            if (keys == null || keys.Length == 0)
                return fallback;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (TryGet(source, key, out int value))
                    return value;
            }
            return fallback;
        }

        public static float Float(
            IDictionary<string, object> source,
            float fallback,
            params string[] keys
        )
        {
            if (keys == null || keys.Length == 0)
                return fallback;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (TryGet(source, key, out float value))
                    return value;
            }
            return fallback;
        }

        public static bool Bool(IDictionary<string, object> source, string key, bool fallback)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
                return fallback;
            if (!source.TryGetValue(key, out var raw))
                return fallback;
            switch (raw)
            {
                case bool b:
                    return b;
                case string s when bool.TryParse(s, out var parsed):
                    return parsed;
                case string s
                    when int.TryParse(
                        s,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedInt
                    ):
                    return parsedInt != 0;
                case int i:
                    return i != 0;
                case long l:
                    return l != 0;
                case float f:
                    return !Mathf.Approximately(f, 0f);
                case double d:
                    return Math.Abs(d) > double.Epsilon;
                case decimal m:
                    return m != 0m;
                default:
                    return fallback;
            }
        }

        public static string String(IDictionary<string, object> source, string key, string fallback)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
                return fallback;
            if (!source.TryGetValue(key, out var raw))
                return fallback;
            if (raw is string sRaw)
                return string.IsNullOrWhiteSpace(sRaw) ? fallback : sRaw.Trim();
            try
            {
                var converted = Convert.ToString(raw, CultureInfo.InvariantCulture);
                return string.IsNullOrWhiteSpace(converted) ? fallback : converted;
            }
            catch
            {
                return fallback;
            }
        }

        public static Color Color(IDictionary<string, object> source, string key, Color fallback)
        {
            if (source == null || string.IsNullOrWhiteSpace(key))
                return fallback;
            if (!source.TryGetValue(key, out var raw))
                return fallback;
            return Config.TryParseColor(raw, out var color) ? color : fallback;
        }

        public static float Radius(IDictionary<string, object> source, string key, float fallback)
        {
            return Float(source, key, fallback, 0f);
        }

        public static float Diameter(IDictionary<string, object> source, string key, float fallback)
        {
            return Float(source, key, fallback, 0.0001f);
        }

        public static float Interval(
            IDictionary<string, object> source,
            string key,
            float fallback,
            float min = 0.0001f
        )
        {
            return Float(source, key, fallback, min);
        }

        public static float Duration(
            IDictionary<string, object> source,
            string key,
            float fallback,
            float min = 0f
        )
        {
            return Float(source, key, fallback, min);
        }

        public static int Damage(IDictionary<string, object> source, string key, int fallback)
        {
            return Int(source, key, fallback, 0);
        }

        public static int Count(
            IDictionary<string, object> source,
            int fallback,
            params string[] keys
        )
        {
            int value = Int(source, fallback, keys);
            return Mathf.Max(1, value);
        }

        public static float Speed(IDictionary<string, object> source, string key, float fallback)
        {
            return Float(source, key, fallback);
        }

        public static float Lifetime(
            IDictionary<string, object> source,
            string key,
            float fallback,
            float min = 0f
        )
        {
            return Float(source, key, fallback, min);
        }

        private static bool TryGet(IDictionary<string, object> source, string key, out float value)
        {
            value = default;
            if (source == null || string.IsNullOrWhiteSpace(key))
                return false;
            if (!source.TryGetValue(key, out var raw))
                return false;
            switch (raw)
            {
                case float f:
                    value = f;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case double d:
                    value = (float)d;
                    return true;
                case decimal m:
                    value = (float)m;
                    return true;
                case string s
                    when float.TryParse(
                        s,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    ):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGet(IDictionary<string, object> source, string key, out int value)
        {
            value = default;
            if (source == null || string.IsNullOrWhiteSpace(key))
                return false;
            if (!source.TryGetValue(key, out var raw))
                return false;
            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    value = (int)l;
                    return true;
                case float f:
                    value = Mathf.RoundToInt(f);
                    return true;
                case double d when d >= int.MinValue && d <= int.MaxValue:
                    value = (int)Math.Round(d, MidpointRounding.AwayFromZero);
                    return true;
                case decimal m when m >= int.MinValue && m <= int.MaxValue:
                    value = (int)Math.Round(m, MidpointRounding.AwayFromZero);
                    return true;
                case string s
                    when int.TryParse(
                        s,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    ):
                    value = parsed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
