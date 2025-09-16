using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Corruption
{
    public static class DrainMechanicSettings
    {
        public static void Apply(DrainMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (TryGet<float>(s, "lifeStealRatio", out var ratio))
                comp.lifeStealRatio = Mathf.Clamp01(ratio);
            if (TryGet<float>(s, "LifeStealPercent", out var pct))
                comp.lifeStealRatio = Mathf.Clamp01(pct);
            if (TryGet<float>(s, "lifeStealChance", out var chance))
                comp.lifeStealChance = Mathf.Clamp01(chance);
            if (TryGet<float>(s, "LifeStealChancePercent", out var chancePct))
                comp.lifeStealChance = Mathf.Clamp01(chancePct);
            if (TryGet<bool>(s, "debugLogs", out var dl))
                comp.debugLogs = dl;
        }

        private static bool TryGet<T>(IDictionary<string, object> s, string key, out T value)
        {
            value = default;
            if (s.TryGetValue(key, out var raw))
            {
                try
                {
                    if (raw is T tv)
                    {
                        value = tv;
                        return true;
                    }
                    if (
                        typeof(T) == typeof(float)
                        && raw is string fs
                        && float.TryParse(fs, out var f)
                    )
                    {
                        value = (T)(object)f;
                        return true;
                    }
                    if (
                        typeof(T) == typeof(bool)
                        && raw is string bs
                        && bool.TryParse(bs, out var b)
                    )
                    {
                        value = (T)(object)b;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
