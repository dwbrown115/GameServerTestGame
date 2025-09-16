using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Helper to apply JSON-like settings to AuraMechanic.
    public static class AuraMechanicSettings
    {
        public static void Apply(AuraMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null) return;
            if (TryGet<float>(s, "radius", out var r)) comp.radius = Mathf.Max(0f, r);
            if (TryGet<int>(s, "damagePerInterval", out var dpi)) comp.damagePerInterval = Mathf.Max(0, dpi);
            if (TryGet<float>(s, "interval", out var iv)) comp.interval = Mathf.Max(0.01f, iv);
            if (TryGet<bool>(s, "centerOnTarget", out var cot)) comp.centerOnTarget = cot;
            if (TryGet<bool>(s, "excludePlayer", out var ep)) comp.excludePlayer = ep;
            if (TryGet<bool>(s, "requireMobTag", out var rm)) comp.requireMobTag = rm;
            if (TryGet<bool>(s, "showVisualization", out var sv)) comp.showVisualization = sv;
            if (TryGet<string>(s, "spriteColor", out var sc) && ColorUtils.TryParse(sc, out var col)) comp.vizColor = col;
            if (TryGet<Color>(s, "vizColor", out var vc)) comp.vizColor = vc;
            if (TryGet<int>(s, "vizSortingOrder", out var vso)) comp.vizSortingOrder = vso;
            if (TryGet<bool>(s, "debugLogs", out var dl)) comp.debugLogs = dl;
        }

        private static bool TryGet<T>(IDictionary<string, object> s, string key, out T value)
        {
            value = default;
            if (s.TryGetValue(key, out var raw))
            {
                try
                {
                    if (raw is T tv) { value = tv; return true; }
                    if (typeof(T) == typeof(float) && raw is string fs && float.TryParse(fs, out var f)) { value = (T)(object)f; return true; }
                    if (typeof(T) == typeof(int) && raw is string isv && int.TryParse(isv, out var i)) { value = (T)(object)i; return true; }
                    if (typeof(T) == typeof(bool) && raw is string bs && bool.TryParse(bs, out var b)) { value = (T)(object)b; return true; }
                    if (typeof(T) == typeof(Color) && raw is string cs && ColorUtils.TryParse(cs, out var c)) { value = (T)(object)c; return true; }
                }
                catch { }
            }
            return false;
        }
    }
}
