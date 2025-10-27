using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Applies JSON-driven settings onto ThrowMovementMechanic instances.
    public static class ThrowMovementMechanicSettings
    {
        public static void Apply(ThrowMovementMechanic comp, IDictionary<string, object> settings)
        {
            if (comp == null || settings == null)
                return;

            comp.initialSpeed = MechanicSettingNormalizer.Speed(
                settings,
                "initialSpeed",
                comp.initialSpeed
            );
            comp.initialSpeed = MechanicSettingNormalizer.Speed(
                settings,
                "throwInitialSpeed",
                comp.initialSpeed
            );
            comp.stopAfterSeconds = MechanicSettingNormalizer.Duration(
                settings,
                "stopAfterSeconds",
                comp.stopAfterSeconds
            );
            comp.stopAfterSeconds = MechanicSettingNormalizer.Duration(
                settings,
                "throwDuration",
                comp.stopAfterSeconds
            );
            comp.alignToVelocity = MechanicSettingNormalizer.Bool(
                settings,
                "alignToVelocity",
                comp.alignToVelocity
            );
            comp.alignToVelocity = MechanicSettingNormalizer.Bool(
                settings,
                "throwAlignToVelocity",
                comp.alignToVelocity
            );
            comp.zeroVelocityOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "zeroVelocityOnStop",
                comp.zeroVelocityOnStop
            );
            comp.zeroVelocityOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "throwZeroVelocityOnStop",
                comp.zeroVelocityOnStop
            );
            comp.disableOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "disableOnStop",
                comp.disableOnStop
            );
            comp.disableOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "throwDisableOnStop",
                comp.disableOnStop
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(settings, "debugLogs", comp.debugLogs);
            comp.detachOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "detachOnStop",
                comp.detachOnStop
            );
            comp.detachOnStart = MechanicSettingNormalizer.Bool(
                settings,
                "detachOnStart",
                comp.detachOnStart
            );
            comp.detachOnStart = MechanicSettingNormalizer.Bool(
                settings,
                "throwDetachOnStart",
                comp.detachOnStart
            );
            comp.randomizeDirectionOnStart = MechanicSettingNormalizer.Bool(
                settings,
                "randomizeDirectionOnStart",
                comp.randomizeDirectionOnStart
            );
            comp.randomizeDirectionOnStart = MechanicSettingNormalizer.Bool(
                settings,
                "throwRandomizeDirection",
                comp.randomizeDirectionOnStart
            );

            comp.direction = ParseDirection(
                settings,
                comp.direction,
                "direction",
                "throwDirection"
            );
        }

        private static Vector2 ParseDirection(
            IDictionary<string, object> settings,
            Vector2 fallback,
            params string[] keys
        )
        {
            if (settings == null || keys == null)
                return fallback;

            foreach (var key in keys)
            {
                string token = MechanicSettingNormalizer.String(settings, key, null);
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                switch (token.Trim().ToLowerInvariant())
                {
                    case "up":
                    case "+y":
                        return Vector2.up;
                    case "down":
                    case "-y":
                        return Vector2.down;
                    case "left":
                    case "-x":
                        return Vector2.left;
                    case "right":
                    case "+x":
                        return Vector2.right;
                }
            }

            return fallback;
        }
    }
}
