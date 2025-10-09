using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class StrikeMechanicSettings
    {
        public static void Apply(StrikeMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (TryGet<float>(s, "interval", out var iv))
                comp.interval = Mathf.Max(0.01f, iv);
            if (TryGet<int>(s, "damagePerInterval", out var dmg))
                comp.damagePerInterval = Mathf.Max(0, dmg);
            if (TryGet<int>(s, "damage", out var dmg2))
                comp.damagePerInterval = Mathf.Max(0, dmg2);
            if (TryGet<bool>(s, "showVisualization", out var viz))
                comp.showVisualization = viz;
            if (TryGet<Color>(s, "vizColor", out var col))
                comp.vizColor = col;
            if (TryGet<string>(s, "spriteColor", out var colStr))
            {
                if (ColorUtils.TryParse(colStr, out var c))
                    comp.vizColor = c;
            }
            if (TryGet<bool>(s, "debugLogs", out var dl))
                comp.debugLogs = dl;
        }

        private static bool TryGet<T>(IDictionary<string, object> s, string key, out T value)
        {
            value = default;
            if (!s.TryGetValue(key, out var raw))
                return false;
            try
            {
                if (raw is T tv)
                {
                    value = tv;
                    return true;
                }
                if (typeof(T) == typeof(int))
                {
                    if (raw is float f)
                    {
                        value = (T)(object)Mathf.RoundToInt(f);
                        return true;
                    }
                    if (raw is string si && int.TryParse(si, out var iv))
                    {
                        value = (T)(object)iv;
                        return true;
                    }
                }
                if (
                    typeof(T) == typeof(float)
                    && raw is string fs
                    && float.TryParse(fs, out var fv)
                )
                {
                    value = (T)(object)fv;
                    return true;
                }
                if (typeof(T) == typeof(bool) && raw is string bs && bool.TryParse(bs, out var bv))
                {
                    value = (T)(object)bv;
                    return true;
                }
                if (
                    typeof(T) == typeof(Color)
                    && raw is string cs
                    && ColorUtils.TryParse(cs, out var cv)
                )
                {
                    value = (T)(object)cv;
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
