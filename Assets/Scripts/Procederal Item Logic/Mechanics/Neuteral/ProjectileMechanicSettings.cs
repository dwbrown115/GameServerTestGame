using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class ProjectileMechanicSettings
    {
        public static void Apply(ProjectileMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            if (TryGet<Vector2>(s, "direction", out var dir))
                comp.direction = dir;
            if (TryGet<float>(s, "speed", out var spd))
                comp.speed = spd;
            if (TryGet<int>(s, "damage", out var dmg))
                comp.damage = dmg;
            if (TryGet<bool>(s, "disableSelfSpeed", out var dss))
                comp.disableSelfSpeed = dss;
            if (TryGet<bool>(s, "requireMobTag", out var rmt))
                comp.requireMobTag = rmt;
            if (TryGet<bool>(s, "excludeOwner", out var eo))
                comp.excludeOwner = eo;
            if (TryGet<bool>(s, "destroyOnHit", out var doh))
                comp.destroyOnHit = doh;
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
                        typeof(T) == typeof(int)
                        && raw is string isv
                        && int.TryParse(isv, out var i)
                    )
                    {
                        value = (T)(object)i;
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
