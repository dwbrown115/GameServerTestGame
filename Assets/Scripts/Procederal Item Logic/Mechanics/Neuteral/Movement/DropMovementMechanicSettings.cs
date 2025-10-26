using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Applies JSON-friendly settings to DropMovementMechanic instances.
    public static class DropMovementMechanicSettings
    {
        public static void Apply(DropMovementMechanic comp, IDictionary<string, object> settings)
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
                "dropInitialSpeed",
                comp.initialSpeed
            );
            comp.gravity = MechanicSettingNormalizer.Float(settings, "gravity", comp.gravity);
            comp.gravity = MechanicSettingNormalizer.Float(settings, "dropGravity", comp.gravity);
            comp.maxSpeed = MechanicSettingNormalizer.Float(settings, "maxSpeed", comp.maxSpeed);
            comp.maxSpeed = MechanicSettingNormalizer.Float(
                settings,
                "dropMaxSpeed",
                comp.maxSpeed
            );
            comp.stopAfterSeconds = MechanicSettingNormalizer.Duration(
                settings,
                "stopAfterSeconds",
                comp.stopAfterSeconds
            );
            comp.stopAfterSeconds = MechanicSettingNormalizer.Duration(
                settings,
                "dropDuration",
                comp.stopAfterSeconds
            );
            comp.zeroVelocityOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "zeroVelocityOnStop",
                comp.zeroVelocityOnStop
            );
            comp.zeroVelocityOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "dropZeroVelocityOnStop",
                comp.zeroVelocityOnStop
            );
            comp.disableOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "disableOnStop",
                comp.disableOnStop
            );
            comp.disableOnStop = MechanicSettingNormalizer.Bool(
                settings,
                "dropDisableOnStop",
                comp.disableOnStop
            );
            comp.alignToDirection = MechanicSettingNormalizer.Bool(
                settings,
                "alignToDirection",
                comp.alignToDirection
            );
            comp.alignToDirection = MechanicSettingNormalizer.Bool(
                settings,
                "dropAlignToDirection",
                comp.alignToDirection
            );
            comp.debugLogs = MechanicSettingNormalizer.Bool(settings, "debugLogs", comp.debugLogs);

            comp.direction = ParseDirection(settings, comp.direction, "direction", "dropDirection");
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
