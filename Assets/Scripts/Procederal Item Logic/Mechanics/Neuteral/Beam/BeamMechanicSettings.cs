using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class BeamMechanicSettings
    {
        public static void Apply(BeamMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            // maxDistance deprecated – ignored if present
            if (TryGet<float>(s, "speed", out var sp))
                comp.extendSpeed = Mathf.Max(0f, sp); // alias from projectile
            if (TryGet<float>(s, "extendSpeed", out var es))
                comp.extendSpeed = Mathf.Max(0f, es);
            if (TryGet<string>(s, "direction", out var dir))
                comp.direction = dir;

            // Width: allow either beamWidth or projectile-style radius
            if (TryGet<float>(s, "beamWidth", out var bw))
                comp.beamWidth = Mathf.Max(0f, bw);
            else if (TryGet<float>(s, "radius", out var rad))
                comp.beamWidth = Mathf.Max(0f, rad * 2f);

            // Damage model (continuous via interval ticks)
            if (TryGet<float>(s, "interval", out var it))
                comp.interval = Mathf.Max(0.01f, it);
            if (TryGet<int>(s, "damagePerInterval", out var dpi))
                comp.damagePerInterval = Mathf.Max(0, dpi);
            if (TryGet<int>(s, "damage", out var dmg))
                comp.damagePerInterval = Mathf.Max(0, dmg); // allow reuse of projectile 'damage'

            if (TryGet<bool>(s, "requireMobTag", out var rmt))
                comp.requireMobTag = rmt;
            if (TryGet<bool>(s, "excludeOwner", out var eo))
                comp.excludeOwner = eo;

            if (TryGet<bool>(s, "showVisualization", out var sv))
                comp.showVisualization = sv;
            if (
                TryGet<string>(s, "spriteColor", out var sc) && ColorUtils.TryParse(sc, out var col)
            )
                comp.vizColor = col;
            if (TryGet<Color>(s, "vizColor", out var vc))
                comp.vizColor = vc;
            if (TryGet<int>(s, "vizSortingOrder", out var vso))
                comp.vizSortingOrder = vso;
            if (TryGet<bool>(s, "debugLogs", out var dl))
                comp.debugLogs = dl;

            // Lifetime & redirect behavior
            if (TryGet<float>(s, "lifetime", out var lt))
                comp.lifetime = Mathf.Max(0f, lt);
            if (TryGet<bool>(s, "preserveHeadOnRedirect", out var phr))
                comp.preserveHeadOnRedirect = phr;
            else if (TryGet<bool>(s, "preserveTipOnRedirect", out var ptr))
                comp.preserveHeadOnRedirect = ptr; // backward compatibility layer 1
            else if (TryGet<bool>(s, "preserveTipOnBounce", out var ptb))
                comp.preserveHeadOnRedirect = ptb; // legacy legacy
            if (TryGet<bool>(s, "anchorTailToPlayer", out var atp))
                comp.anchorTailToPlayer = atp;
            if (TryGet<bool>(s, "segmentOnRedirect", out var sor))
                comp.segmentOnRedirect = sor;
            else if (TryGet<bool>(s, "segmentOnBounce", out var sob))
                comp.segmentOnRedirect = sob; // backward compatibility
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
                    if (
                        typeof(T) == typeof(Color)
                        && raw is string cs
                        && ColorUtils.TryParse(cs, out var c)
                    )
                    {
                        value = (T)(object)c;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }
    }
}
