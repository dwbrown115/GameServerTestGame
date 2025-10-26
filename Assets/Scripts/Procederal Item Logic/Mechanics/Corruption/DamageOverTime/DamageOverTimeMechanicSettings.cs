using System.Collections.Generic;
using Game.Procederal.Core;

namespace Mechanics.Corruption
{
    /// Helper that bridges JSON settings onto DamageOverTimeMechanic components.
    public static class DamageOverTimeMechanicSettings
    {
        public static void Apply(DamageOverTimeMechanic comp, IDictionary<string, object> settings)
        {
            if (comp == null || settings == null)
                return;

            comp.damagePerTick = MechanicSettingNormalizer.Damage(
                settings,
                "damagePerTick",
                comp.damagePerTick
            );
            comp.interval = MechanicSettingNormalizer.Interval(
                settings,
                comp.interval,
                0.0001f,
                "damageInterval",
                "interval"
            );
            comp.duration = MechanicSettingNormalizer.Duration(settings, "duration", comp.duration);
            comp.allowStacking = MechanicSettingNormalizer.Bool(
                settings,
                "allowStacking",
                comp.allowStacking
            );
            comp.effectId = MechanicSettingNormalizer.String(settings, "effectId", comp.effectId);
            comp.debugLogs = MechanicSettingNormalizer.Bool(settings, "debugLogs", comp.debugLogs);
        }
    }
}
