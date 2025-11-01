using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    // Shared helper so primary builders can attach Drop/Throw movement mechanics
    // when the merged settings specify a "movementMode".
    public static class BuilderMovementHelper
    {
        public enum MovementSelection
        {
            None = 0,
            Default,
            Drop,
            Throw,
        }

        public static MovementSelection GetMovementMode(Dictionary<string, object> merged)
        {
            if (merged == null)
                return MovementSelection.None;

            string mode = MechanicSettingNormalizer.String(merged, "movementMode", string.Empty);
            if (string.IsNullOrWhiteSpace(mode))
                return MovementSelection.None;

            string normalized = mode.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "drop":
                case "dropdown":
                    return MovementSelection.Drop;
                case "throw":
                case "lob":
                    return MovementSelection.Throw;
                case "default":
                case "none":
                case "static":
                    return MovementSelection.Default;
                default:
                    return MovementSelection.Default;
            }
        }

        public static MovementSelection OverrideWithChildBehavior(
            Dictionary<string, object> merged,
            MovementSelection current,
            ChildBehaviorSelection childBehavior
        )
        {
            if (childBehavior == ChildBehaviorSelection.Unspecified)
                return current;

            MovementSelection forced = current;
            switch (childBehavior)
            {
                case ChildBehaviorSelection.Drop:
                    forced = MovementSelection.Drop;
                    break;
                case ChildBehaviorSelection.Throw:
                    forced = MovementSelection.Throw;
                    break;
                case ChildBehaviorSelection.Shoot:
                    forced = MovementSelection.Default;
                    break;
                case ChildBehaviorSelection.None:
                    forced = MovementSelection.None;
                    break;
            }

            if (merged != null)
            {
                if (forced == MovementSelection.None)
                {
                    if (merged.ContainsKey("movementMode"))
                        merged.Remove("movementMode");
                }
                else
                {
                    merged["movementMode"] = forced.ToString();
                }
            }

            return forced;
        }

        public static void AttachMovementIfRequested(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ItemParams p,
            Game.Procederal.ProcederalItemGenerator gen,
            List<UnifiedChildBuilder.MechanicSpec> mechanics
        )
        {
            if (merged == null || mechanics == null)
                return;

            switch (GetMovementMode(merged))
            {
                case MovementSelection.Drop:
                    mechanics.Add(
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "DropMovementMechanic",
                            Settings = BuildDropSettings(merged, root, gen, p).ToArray(),
                            SkipIfPresent = true,
                        }
                    );
                    break;
                case MovementSelection.Throw:
                    mechanics.Add(
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "ThrowMovementMechanic",
                            Settings = BuildThrowSettings(merged, root, gen, p).ToArray(),
                            SkipIfPresent = true,
                        }
                    );
                    break;
                default:
                    break;
            }
        }

        public static List<UnifiedChildBuilder.MechanicSpec> GetMovementMechanicSpecs(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ItemParams p,
            Game.Procederal.ProcederalItemGenerator gen
        )
        {
            var list = new List<UnifiedChildBuilder.MechanicSpec>();
            var selection = GetMovementMode(merged);
            if (selection == MovementSelection.Drop)
            {
                list.Add(
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "DropMovementMechanic",
                        Settings = BuildDropSettings(merged, root, gen, p).ToArray(),
                        SkipIfPresent = true,
                    }
                );
            }
            else if (selection == MovementSelection.Throw)
            {
                list.Add(
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "ThrowMovementMechanic",
                        Settings = BuildThrowSettings(merged, root, gen, p).ToArray(),
                        SkipIfPresent = true,
                    }
                );
            }

            return list;
        }

        public static bool ShouldDetachFromParent(Dictionary<string, object> merged)
        {
            return ShouldDetachFromParent(GetMovementMode(merged));
        }

        public static bool ShouldDetachFromParent(MovementSelection selection)
        {
            return selection == MovementSelection.Drop;
        }

        private static List<(string key, object val)> BuildDropSettings(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemParams p
        )
        {
            Vector2 dir = ResolveDirection(merged, "dropDirection", Vector2.down, root);

            var list = new List<(string key, object val)>()
            {
                ("direction", dir.normalized),
                ("debugLogs", (p != null && p.debugLogs) || gen.debugLogs),
            };

            if (HasAnyKey(merged, "initialSpeed", "dropInitialSpeed"))
            {
                float initialSpeed = MechanicSettingNormalizer.Speed(
                    merged,
                    "dropInitialSpeed",
                    MechanicSettingNormalizer.Speed(merged, "initialSpeed", 0f)
                );
                list.Add(("initialSpeed", Mathf.Max(0f, initialSpeed)));
            }

            if (HasAnyKey(merged, "gravity", "dropGravity"))
            {
                float gravity = MechanicSettingNormalizer.Float(
                    merged,
                    "dropGravity",
                    MechanicSettingNormalizer.Float(merged, "gravity", 25f)
                );
                list.Add(("gravity", Mathf.Max(0f, gravity)));
            }

            if (HasAnyKey(merged, "maxSpeed", "dropMaxSpeed"))
            {
                float maxSpeed = MechanicSettingNormalizer.Float(
                    merged,
                    "dropMaxSpeed",
                    MechanicSettingNormalizer.Float(merged, "maxSpeed", 20f)
                );
                list.Add(("maxSpeed", Mathf.Max(0f, maxSpeed)));
            }

            if (HasAnyKey(merged, "stopAfterSeconds", "dropDuration"))
            {
                float stopAfter = MechanicSettingNormalizer.Duration(
                    merged,
                    "dropDuration",
                    MechanicSettingNormalizer.Duration(merged, "stopAfterSeconds", 0.45f)
                );
                list.Add(("stopAfterSeconds", Mathf.Max(0f, stopAfter)));
            }

            if (HasAnyKey(merged, "zeroVelocityOnStop", "dropZeroVelocityOnStop"))
            {
                bool zeroOnStop = MechanicSettingNormalizer.Bool(
                    merged,
                    "dropZeroVelocityOnStop",
                    MechanicSettingNormalizer.Bool(merged, "zeroVelocityOnStop", true)
                );
                list.Add(("zeroVelocityOnStop", zeroOnStop));
            }

            if (HasAnyKey(merged, "disableOnStop", "dropDisableOnStop"))
            {
                bool disableOnStop = MechanicSettingNormalizer.Bool(
                    merged,
                    "dropDisableOnStop",
                    MechanicSettingNormalizer.Bool(merged, "disableOnStop", true)
                );
                list.Add(("disableOnStop", disableOnStop));
            }

            return list;
        }

        private static List<(string key, object val)> BuildThrowSettings(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemParams p
        )
        {
            bool hasExplicitDirection = HasAnyKey(merged, "throwDirection", "direction");
            Vector2 dir = ResolveDirection(
                merged,
                "throwDirection",
                root != null ? (Vector2)root.right : Vector2.right,
                root
            );

            var list = new List<(string key, object val)>()
            {
                ("direction", dir.normalized),
                ("debugLogs", (p != null && p.debugLogs) || gen.debugLogs),
            };

            if (
                !hasExplicitDirection
                && !HasAnyKey(merged, "randomizeDirectionOnStart", "throwRandomizeDirection")
            )
            {
                list.Add(("randomizeDirectionOnStart", true));
            }

            if (HasAnyKey(merged, "initialSpeed", "throwInitialSpeed"))
            {
                float resolvedSpeed = MechanicSettingNormalizer.Speed(
                    merged,
                    "throwInitialSpeed",
                    MechanicSettingNormalizer.Speed(merged, "initialSpeed", 12f)
                );
                list.Add(("initialSpeed", Mathf.Max(0f, resolvedSpeed)));
            }

            if (HasAnyKey(merged, "stopAfterSeconds", "throwDuration"))
            {
                float stopAfter = MechanicSettingNormalizer.Duration(
                    merged,
                    "throwDuration",
                    MechanicSettingNormalizer.Duration(merged, "stopAfterSeconds", 0.65f)
                );
                list.Add(("stopAfterSeconds", Mathf.Max(0f, stopAfter)));
            }

            if (HasAnyKey(merged, "alignToVelocity", "throwAlignToVelocity"))
            {
                bool align = MechanicSettingNormalizer.Bool(
                    merged,
                    "throwAlignToVelocity",
                    MechanicSettingNormalizer.Bool(merged, "alignToVelocity", true)
                );
                list.Add(("alignToVelocity", align));
            }

            if (HasAnyKey(merged, "zeroVelocityOnStop", "throwZeroVelocityOnStop"))
            {
                bool zeroOnStop = MechanicSettingNormalizer.Bool(
                    merged,
                    "throwZeroVelocityOnStop",
                    MechanicSettingNormalizer.Bool(merged, "zeroVelocityOnStop", true)
                );
                list.Add(("zeroVelocityOnStop", zeroOnStop));
            }

            if (HasAnyKey(merged, "disableOnStop", "throwDisableOnStop"))
            {
                bool disableOnStop = MechanicSettingNormalizer.Bool(
                    merged,
                    "throwDisableOnStop",
                    MechanicSettingNormalizer.Bool(merged, "disableOnStop", false)
                );
                list.Add(("disableOnStop", disableOnStop));
            }

            if (HasAnyKey(merged, "detachOnStop"))
            {
                bool detachOnStop = MechanicSettingNormalizer.Bool(merged, "detachOnStop", true);
                list.Add(("detachOnStop", detachOnStop));
            }

            return list;
        }

        private static Vector2 ResolveDirection(
            IDictionary<string, object> merged,
            string key,
            Vector2 fallback,
            Transform root
        )
        {
            string value = MechanicSettingNormalizer.String(merged, key, null);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            switch (value.Trim().ToLowerInvariant())
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
                case "forward":
                case "ownerforward":
                case "owner":
                    if (root != null)
                        return root.right;
                    break;
                case "back":
                case "backward":
                    if (root != null)
                        return -root.right;
                    break;
            }

            return fallback;
        }

        private static bool HasAnyKey(IDictionary<string, object> source, params string[] keys)
        {
            if (source == null || keys == null || keys.Length == 0)
                return false;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (source.ContainsKey(key))
                    return true;
            }
            return false;
        }
    }
}
