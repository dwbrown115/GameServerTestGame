using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    public static class StrikeMechanicSettings
    {
        public static void Apply(StrikeMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            comp.interval = MechanicSettingNormalizer.Interval(s, "interval", comp.interval);
            comp.damagePerInterval = MechanicSettingNormalizer.Damage(
                s,
                "damagePerInterval",
                comp.damagePerInterval
            );
            comp.damagePerInterval = MechanicSettingNormalizer.Damage(
                s,
                "damage",
                comp.damagePerInterval
            );
            comp.showVisualization = MechanicSettingNormalizer.Bool(
                s,
                "showVisualization",
                comp.showVisualization
            );
            comp.vizColor = MechanicSettingNormalizer.Color(s, "spriteColor", comp.vizColor);
            comp.vizColor = MechanicSettingNormalizer.Color(s, "vizColor", comp.vizColor);
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
        }
    }
}
