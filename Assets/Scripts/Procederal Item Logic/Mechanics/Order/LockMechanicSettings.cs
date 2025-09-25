using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Order
{
    public static class LockMechanicSettings
    {
        public static void Apply(LockMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (TryGet<float>(s, "stunTime", out var t))
                comp.stunTime = Mathf.Max(0f, t);
            if (TryGet<float>(s, "StunTime", out var t2))
                comp.stunTime = Mathf.Max(0f, t2);
            if (TryGet<float>(s, "Stun Time", out var t3))
                comp.stunTime = Mathf.Max(0f, t3);
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
