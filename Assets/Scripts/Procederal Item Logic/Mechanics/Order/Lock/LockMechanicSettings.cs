using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Order
{
    public static class LockMechanicSettings
    {
        public static void Apply(LockMechanic comp, IDictionary<string, object> s)
        {
            if (comp == null || s == null)
                return;
            comp.stunTime = MechanicSettingNormalizer.Float(s, "stunTime", comp.stunTime, 0f);
            comp.stunTime = MechanicSettingNormalizer.Float(s, "StunTime", comp.stunTime, 0f);
            comp.stunTime = MechanicSettingNormalizer.Float(s, "Stun Time", comp.stunTime, 0f);
            comp.stunChance = MechanicSettingNormalizer.Float(
                s,
                "stunChance",
                comp.stunChance,
                0f,
                1f
            );
            comp.stunChance = MechanicSettingNormalizer.Float(
                s,
                "StunChance",
                comp.stunChance,
                0f,
                1f
            );
            comp.stunChance = MechanicSettingNormalizer.Float(
                s,
                "Stun Chance",
                comp.stunChance,
                0f,
                1f
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(s, "debugLogs", comp.debugLogs);
        }
    }
}
