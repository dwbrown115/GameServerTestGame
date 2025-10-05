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
            if (raw is string s)
                return ColorUtils.TryParse(s, out color);
            return false;
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
        }
    }
}
