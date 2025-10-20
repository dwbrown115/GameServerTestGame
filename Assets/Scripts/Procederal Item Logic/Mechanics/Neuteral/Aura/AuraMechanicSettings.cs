using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Helper to apply JSON-like settings to AuraMechanic.
    public static class AuraMechanicSettings
    {
        public static void Apply(AuraMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            comp.radius = MechanicSettingNormalizer.Radius(s, "radius", comp.radius);
            comp.damagePerInterval = MechanicSettingNormalizer.Damage(
                s,
                "damagePerInterval",
                comp.damagePerInterval
            );
            comp.interval = MechanicSettingNormalizer.Interval(s, "interval", comp.interval);
            comp.centerOnTarget = MechanicSettingNormalizer.Bool(
                s,
                "centerOnTarget",
                comp.centerOnTarget
            );
            comp.excludePlayer = MechanicSettingNormalizer.Bool(
                s,
                "excludePlayer",
                comp.excludePlayer
            );
            comp.requireMobTag = MechanicSettingNormalizer.Bool(
                s,
                "requireMobTag",
                comp.requireMobTag
            );
            comp.showVisualization = MechanicSettingNormalizer.Bool(
                s,
                "showVisualization",
                comp.showVisualization
            );
            comp.vizColor = MechanicSettingNormalizer.Color(s, "spriteColor", comp.vizColor);
            comp.vizColor = MechanicSettingNormalizer.Color(s, "vizColor", comp.vizColor);
            comp.vizSortingOrder = MechanicSettingNormalizer.Int(
                s,
                "vizSortingOrder",
                comp.vizSortingOrder
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
        }
    }
}
