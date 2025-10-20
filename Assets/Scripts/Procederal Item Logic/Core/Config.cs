using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Core
{
    /// Shared configuration and JSON parsing helpers for Procederal mechanics.
    public static class Config
    {
        public static bool TryParseColor(object raw, out Color color)
        {
            color = Color.white;
            switch (raw)
            {
                case null:
                    return false;
                case Color c:
                    color = c;
                    return true;
                case Color32 c32:
                    color = c32;
                    return true;
                case string s:
                    if (ColorUtils.TryParse(s, out color))
                        return true;
                    return TryParseNamedColor(s, out color);
                default:
                    return false;
            }
        }

        public static void ReadIf(Dictionary<string, object> dict, string key, ref int val)
        {
            if (dict == null || !dict.TryGetValue(key, out var v))
                return;
            if (v is int vi)
                val = vi;
            else if (v is float vf)
                val = Mathf.RoundToInt(vf);
            else if (v is string vs && int.TryParse(vs, out var pi))
                val = pi;
        }

        public static void ReadIf(Dictionary<string, object> dict, string key, ref float val)
        {
            if (dict == null || !dict.TryGetValue(key, out var v))
                return;
            if (v is float vf)
                val = vf;
            else if (v is int vi)
                val = vi;
            else if (v is string vs && float.TryParse(vs, out var pf))
                val = pf;
        }

        public static void ReadIf(Dictionary<string, object> dict, string key, ref bool val)
        {
            if (dict == null || !dict.TryGetValue(key, out var v))
                return;
            if (v is bool vb)
                val = vb;
            else if (v is string vs && bool.TryParse(vs, out var pb))
                val = pb;
            else if (v is string ns && int.TryParse(ns, out var parsedInt))
                val = parsedInt != 0;
            else if (v is int vi)
                val = vi != 0;
            else if (v is long vl)
                val = vl != 0;
            else if (v is float vf)
                val = !Mathf.Approximately(vf, 0f);
            else if (v is double vd)
                val = Math.Abs(vd) > double.Epsilon;
            else if (v is decimal vm)
                val = vm != 0m;
        }

        private static bool TryParseNamedColor(string token, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(token))
                return false;
            switch (token.Trim().ToLowerInvariant())
            {
                case "red":
                    color = Color.red;
                    return true;
                case "green":
                    color = Color.green;
                    return true;
                case "blue":
                    color = Color.blue;
                    return true;
                case "white":
                    color = Color.white;
                    return true;
                case "black":
                    color = Color.black;
                    return true;
                case "yellow":
                    color = Color.yellow;
                    return true;
                case "cyan":
                    color = Color.cyan;
                    return true;
                case "magenta":
                    color = Color.magenta;
                    return true;
                case "gray":
                case "grey":
                    color = Color.gray;
                    return true;
                default:
                    return false;
            }
        }
    }
}
