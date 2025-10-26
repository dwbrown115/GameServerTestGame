using System.Collections.Generic;
using Game.Procederal.Core;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Maps loosely typed dictionaries (typically loaded from JSON) onto <see cref="ChildHealthMechanic"/> instances.
    /// </summary>
    public static class ChildHealthMechanicSettings
    {
        public static void Apply(ChildHealthMechanic comp, IDictionary<string, object> settings)
        {
            if (comp == null || settings == null)
                return;

            var normalized = ToCaseInsensitive(settings);

            if (normalized.ContainsKey("maxHealth"))
            {
                int max = MechanicSettingNormalizer.Int(normalized, "maxHealth", comp.maxHealth, 1);
                comp.maxHealth = max;
                // Ensure current health respects new max if it was previously higher
                comp.SetCurrentHealth(comp.CurrentHealth, clampToMax: true, suppressLog: true);
            }

            if (normalized.ContainsKey("startingHealth"))
            {
                int start = MechanicSettingNormalizer.Int(
                    normalized,
                    "startingHealth",
                    comp.maxHealth,
                    0
                );
                comp.SetCurrentHealth(start, clampToMax: true, suppressLog: true);
            }

            if (normalized.ContainsKey("resetHealthOnEnable"))
                comp.resetHealthOnEnable = MechanicSettingNormalizer.Bool(
                    normalized,
                    "resetHealthOnEnable",
                    comp.resetHealthOnEnable
                );

            if (normalized.ContainsKey("destroyOnDepleted"))
                comp.destroyOnDepleted = MechanicSettingNormalizer.Bool(
                    normalized,
                    "destroyOnDepleted",
                    comp.destroyOnDepleted
                );

            if (normalized.ContainsKey("disableGameObjectOnDepleted"))
                comp.disableGameObjectOnDepleted = MechanicSettingNormalizer.Bool(
                    normalized,
                    "disableGameObjectOnDepleted",
                    comp.disableGameObjectOnDepleted
                );

            if (normalized.ContainsKey("disableMechanicOnDepleted"))
                comp.disableMechanicOnDepleted = MechanicSettingNormalizer.Bool(
                    normalized,
                    "disableMechanicOnDepleted",
                    comp.disableMechanicOnDepleted
                );

            if (normalized.ContainsKey("disableCollidersOnDepleted"))
                comp.disableCollidersOnDepleted = MechanicSettingNormalizer.Bool(
                    normalized,
                    "disableCollidersOnDepleted",
                    comp.disableCollidersOnDepleted
                );

            if (normalized.ContainsKey("sendMessageOnDepleted"))
                comp.sendMessageOnDepleted = MechanicSettingNormalizer.String(
                    normalized,
                    "sendMessageOnDepleted",
                    comp.sendMessageOnDepleted
                );

            if (normalized.ContainsKey("sendMessageToOwner"))
                comp.sendMessageToOwner = MechanicSettingNormalizer.Bool(
                    normalized,
                    "sendMessageToOwner",
                    comp.sendMessageToOwner
                );

            if (normalized.ContainsKey("sendMessageToPayload"))
                comp.sendMessageToPayload = MechanicSettingNormalizer.Bool(
                    normalized,
                    "sendMessageToPayload",
                    comp.sendMessageToPayload
                );

            if (normalized.ContainsKey("debugLogs"))
                comp.debugLogs = MechanicSettingNormalizer.Bool(
                    normalized,
                    "debugLogs",
                    comp.debugLogs
                );
        }

        private static Dictionary<string, object> ToCaseInsensitive(
            IDictionary<string, object> source
        )
        {
            if (
                source is Dictionary<string, object> dict
                && dict.Comparer == System.StringComparer.OrdinalIgnoreCase
            )
            {
                return dict;
            }

            var result = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kv in source)
            {
                if (kv.Key == null)
                    continue;
                result[kv.Key] = kv.Value;
            }
            return result;
        }
    }
}
