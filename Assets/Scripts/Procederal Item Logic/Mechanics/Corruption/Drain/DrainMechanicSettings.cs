using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Corruption
{
    public static class DrainMechanicSettings
    {
        public static void Apply(DrainMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            comp.lifeStealRatio = MechanicSettingNormalizer.Float(
                s,
                "lifeStealRatio",
                comp.lifeStealRatio,
                0f,
                1f
            );
            comp.lifeStealRatio = MechanicSettingNormalizer.Float(
                s,
                "LifeStealPercent",
                comp.lifeStealRatio,
                0f,
                1f
            );
            comp.lifeStealChance = MechanicSettingNormalizer.Float(
                s,
                "lifeStealChance",
                comp.lifeStealChance,
                0f,
                1f
            );
            comp.lifeStealChance = MechanicSettingNormalizer.Float(
                s,
                "LifeStealChancePercent",
                comp.lifeStealChance,
                0f,
                1f
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
        }
    }
}
