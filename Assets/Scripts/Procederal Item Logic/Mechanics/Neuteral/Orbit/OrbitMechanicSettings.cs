using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class OrbitMechanicSettings
    {
        public static void Apply(OrbitMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (TryGet<float>(s, "angularSpeedDeg", out var asd))
                comp.angularSpeedDeg = asd;
            if (TryGet<float>(s, "radius", out var r))
                comp.radius = Mathf.Max(0f, r);
            if (TryGet<float>(s, "startAngleDeg", out var sad))
                comp.startAngleDeg = sad;
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
                    if (
                        typeof(T) == typeof(int)
                        && raw is string isv
                        && int.TryParse(isv, out var i)
                    )
                    {
                        value = (T)(object)i;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
