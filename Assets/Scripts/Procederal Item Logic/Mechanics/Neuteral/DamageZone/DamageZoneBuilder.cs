using System.Collections;
using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    /// Builder for the Damage Zone primary mechanic.
    /// Composes the zone payload, attaches the mechanic, and optionally wires movement behaviors.
    public class DamageZoneBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.DamageZone;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var merged = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("DamageZone"),
                gen.CollectSecondarySettings(instruction)
            );

            float radius = MechanicSettingNormalizer.Radius(merged, "radius", p.damageZoneRadius);
            float interval = MechanicSettingNormalizer.Interval(
                merged,
                p.damageZoneInterval,
                0.05f,
                "damageInterval",
                "interval"
            );
            int damage = MechanicSettingNormalizer.Damage(
                merged,
                "damagePerInterval",
                p.damageZoneDamage
            );
            float lifetime = MechanicSettingNormalizer.Lifetime(
                merged,
                "lifetimeSeconds",
                p.damageZoneLifetime
            );
            float initialDelay = MechanicSettingNormalizer.Duration(merged, "initialDelay", 0f);
            bool showViz = MechanicSettingNormalizer.Bool(merged, "showVisualization", true);
            Color vizColor = MechanicSettingNormalizer.Color(
                merged,
                "spriteColor",
                new Color(1f, 0.35f, 0f, 0.2f)
            );
            vizColor = MechanicSettingNormalizer.Color(merged, "vizColor", vizColor);
            string spriteType = MechanicSettingNormalizer.String(merged, "spriteType", "circle");
            int sortingOrder = MechanicSettingNormalizer.Int(merged, "vizSortingOrder", -95);

            bool followOwner = MechanicSettingNormalizer.Bool(merged, "followOwner", false);
            bool followTarget = MechanicSettingNormalizer.Bool(merged, "followTarget", false);
            bool excludeOwner = MechanicSettingNormalizer.Bool(merged, "excludeOwner", true);
            bool requireMobTag = MechanicSettingNormalizer.Bool(merged, "requireMobTag", true);
            bool destroyOnExpire = MechanicSettingNormalizer.Bool(merged, "destroyOnExpire", true);
            bool disableOnExpire = MechanicSettingNormalizer.Bool(merged, "disableOnExpire", false);

            float offsetX = MechanicSettingNormalizer.Float(
                merged,
                0f,
                "offsetX",
                "offset_x",
                "worldOffsetX",
                "world_offset_x"
            );
            float offsetY = MechanicSettingNormalizer.Float(
                merged,
                0f,
                "offsetY",
                "offset_y",
                "worldOffsetY",
                "world_offset_y"
            );
            Vector2 offset = new Vector2(offsetX, offsetY);

            int layerMask = MechanicSettingNormalizer.Int(
                merged,
                (~0),
                "targetLayerMask",
                "targetLayers",
                "layerMask"
            );

            var zoneSettings = new List<(string key, object val)>
            {
                ("radius", Mathf.Max(0f, radius)),
                ("damageInterval", Mathf.Max(0.05f, interval)),
                ("interval", Mathf.Max(0.05f, interval)),
                ("damagePerInterval", Mathf.Max(0, damage)),
                ("initialDelay", Mathf.Max(0f, initialDelay)),
                ("lifetimeSeconds", lifetime),
                ("destroyOnExpire", destroyOnExpire),
                ("disableOnExpire", disableOnExpire),
                ("followOwner", followOwner),
                ("followTarget", followTarget),
                ("excludeOwner", excludeOwner),
                ("requireMobTag", requireMobTag),
                ("showVisualization", showViz),
                ("vizColor", vizColor),
                ("spriteColor", vizColor),
                ("vizSortingOrder", sortingOrder),
                ("worldOffset", offset),
                ("targetLayers", (LayerMask)layerMask),
                ("debugLogs", p.debugLogs || gen.debugLogs),
            };

            var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
            {
                new UnifiedChildBuilder.MechanicSpec
                {
                    Name = "DamageZone",
                    Settings = zoneSettings.ToArray(),
                },
            };

            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                merged
            );
            if (p != null)
            {
                movementMode =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.OverrideWithChildBehavior(
                        merged,
                        movementMode,
                        p.childBehavior
                    );
            }
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );

            AttachMovementIfRequested(merged, root.transform, p, gen, mechanics);

            // Allow the generator to request interval spawning instead of a single child.
            string spawnBehavior = MechanicSettingNormalizer
                .String(merged, "spawnBehavior", string.Empty)
                .Trim()
                .ToLowerInvariant();
            if (string.Equals(spawnBehavior, "interval", System.StringComparison.OrdinalIgnoreCase))
            {
                // Configure a GenericIntervalSpawner on the root to spawn DamageZone payloads periodically.
                var spawner =
                    root.GetComponent<Game.Procederal.Api.GenericIntervalSpawner>()
                    ?? root.AddComponent<Game.Procederal.Api.GenericIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = gen.owner != null ? gen.owner : gen.transform;
                spawner.interval = MechanicSettingNormalizer.Interval(
                    merged,
                    1.0f,
                    0.05f,
                    "spawnInterval",
                    "interval"
                );
                spawner.countPerInterval = Mathf.Max(
                    1,
                    MechanicSettingNormalizer.Int(merged, "childrenToSpawn", 1)
                );
                spawner.spawnRadius = MechanicSettingNormalizer.Float(merged, "spawnRadius", 0f);
                spawner.lifetime = MechanicSettingNormalizer.Lifetime(
                    merged,
                    "lifetimeSeconds",
                    lifetime
                );
                spawner.immediateFirstBurst = MechanicSettingNormalizer.Bool(
                    merged,
                    "immediateFirstBurst",
                    false
                );
                spawner.payloadMechanicName = "DamageZone";
                spawner.SetPayloadSettings(zoneSettings.ToArray());
                spawner.parentSpawnedToSpawner = !shouldDetachChildren;
                spawner.createSpriteRenderer = false; // DamageZone handles its own visualization
                spawner.createCollider = false; // AreaDamageMechanicBase queries overlaps without collider
                spawner.autoDestroyPayloads = destroyOnExpire;
                spawner.spriteType = null;
                spawner.customSpritePath = null;
                spawner.spriteColor = vizColor;
                spawner.colliderRadius = Mathf.Max(0.01f, radius);
                spawner.preventOverlap = false;
                spawner.avoidDuplicateNearOwner = true;
                spawner.duplicateMechanicName = "DamageZone";
                spawner.duplicateCheckRadius = Mathf.Max(0.01f, radius);
                spawner.debugLogs = p.debugLogs || gen.debugLogs;

                if (gen.autoApplyCompatibleModifiers)
                {
                    spawner.ClearModifierSpecs();
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);
                }

                // Attach any additional mechanics (e.g., movement) as modifier specs so each spawn receives them.
                if (mechanics != null)
                {
                    for (int i = 0; i < mechanics.Count; i++)
                    {
                        var mechSpec = mechanics[i];
                        if (string.IsNullOrWhiteSpace(mechSpec.Name))
                            continue;
                        // Payload already adds DamageZone via SetPayloadSettings, so only forward extra mechanics.
                        if (
                            string.Equals(
                                mechSpec.Name,
                                "DamageZone",
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue;
                        spawner.AddModifierSpec(
                            mechSpec.Name,
                            mechSpec.Settings ?? System.Array.Empty<(string key, object val)>()
                        );
                    }
                }

                // We created a spawner instead of a static child. Nothing more to do here.
                return;
            }

            bool createCollider = MechanicSettingNormalizer.Bool(merged, "createCollider", false);
            float colliderRadius = MechanicSettingNormalizer.Radius(
                merged,
                "colliderRadius",
                radius
            );

            var spec = new UnifiedChildBuilder.ChildSpec
            {
                ChildName = "DamageZone",
                Parent = root.transform,
                Layer = root.layer,
                LocalScale = Vector3.one,
                Visual = new UnifiedChildBuilder.SpriteSpec
                {
                    Enabled = showViz,
                    SpriteType = showViz ? spriteType : null,
                    Color = vizColor,
                    SortingOrder = sortingOrder,
                },
                Collider = new UnifiedChildBuilder.ColliderSpec
                {
                    Enabled = createCollider,
                    Shape = UnifiedChildBuilder.ColliderShape2D.Circle,
                    Radius = Mathf.Max(0.01f, colliderRadius),
                    IsTrigger = true,
                },
                Rigidbody = new UnifiedChildBuilder.RigidbodySpec
                {
                    Enabled = true,
                    BodyType = RigidbodyType2D.Kinematic,
                    FreezeRotation = true,
                    GravityScale = 0f,
                    Interpolation = RigidbodyInterpolation2D.Interpolate,
                    CollisionDetection = CollisionDetectionMode2D.Continuous,
                },
                Mechanics = mechanics,
                LifetimeSeconds = destroyOnExpire && lifetime > 0f ? lifetime : (float?)null,
            };

            var zone = UnifiedChildBuilder.BuildChild(gen, spec);
            if (shouldDetachChildren)
                zone.transform.SetParent(null, worldPositionStays: true);
            subItems.Add(zone);
        }

        private static void AttachMovementIfRequested(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ItemParams p,
            Game.Procederal.ProcederalItemGenerator gen,
            List<UnifiedChildBuilder.MechanicSpec> mechanics
        )
        {
            if (merged == null || mechanics == null)
                return;

            string mode = MechanicSettingNormalizer.String(merged, "movementMode", string.Empty);
            if (string.IsNullOrWhiteSpace(mode))
                return;

            var dirLower = mode.Trim().ToLowerInvariant();
            switch (dirLower)
            {
                case "drop":
                case "dropdown":
                    mechanics.Add(
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "DropMovementMechanic",
                            Settings = BuildDropSettings(merged, root, gen, p).ToArray(),
                            SkipIfPresent = true,
                        }
                    );
                    break;
                case "throw":
                case "lob":
                    mechanics.Add(
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "ThrowMovementMechanic",
                            Settings = BuildThrowSettings(merged, root, gen, p).ToArray(),
                            SkipIfPresent = true,
                        }
                    );
                    break;
                case "static":
                default:
                    break;
            }
        }

        private static List<(string key, object val)> BuildDropSettings(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemParams p
        )
        {
            float initialSpeed = MechanicSettingNormalizer.Speed(merged, "dropInitialSpeed", 0f);
            float gravity = MechanicSettingNormalizer.Float(merged, "dropGravity", 25f);
            float maxSpeed = MechanicSettingNormalizer.Float(merged, "dropMaxSpeed", 20f);
            float stopAfter = MechanicSettingNormalizer.Duration(merged, "dropDuration", 0.45f);
            bool zeroOnStop = MechanicSettingNormalizer.Bool(
                merged,
                "dropZeroVelocityOnStop",
                true
            );
            bool disableOnStop = MechanicSettingNormalizer.Bool(merged, "dropDisableOnStop", true);
            Vector2 dir = ResolveDirection(merged, "dropDirection", Vector2.down, root);

            return new List<(string key, object val)>
            {
                ("initialSpeed", Mathf.Max(0f, initialSpeed)),
                ("gravity", Mathf.Max(0f, gravity)),
                ("maxSpeed", Mathf.Max(0f, maxSpeed)),
                ("stopAfterSeconds", Mathf.Max(0f, stopAfter)),
                ("direction", dir.normalized),
                ("zeroVelocityOnStop", zeroOnStop),
                ("disableOnStop", disableOnStop),
                ("debugLogs", p.debugLogs || gen.debugLogs),
            };
        }

        private static List<(string key, object val)> BuildThrowSettings(
            Dictionary<string, object> merged,
            Transform root,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemParams p
        )
        {
            float speed = MechanicSettingNormalizer.Speed(merged, "throwInitialSpeed", 12f);
            float gravity = MechanicSettingNormalizer.Float(merged, "throwGravity", 22f);
            float stopAfter = MechanicSettingNormalizer.Duration(merged, "throwDuration", 0.65f);
            float downwardClamp = MechanicSettingNormalizer.Float(merged, "throwMaxFallSpeed", 24f);
            bool align = MechanicSettingNormalizer.Bool(merged, "throwAlignToVelocity", true);
            bool zeroOnStop = MechanicSettingNormalizer.Bool(
                merged,
                "throwZeroVelocityOnStop",
                true
            );
            bool disableOnStop = MechanicSettingNormalizer.Bool(
                merged,
                "throwDisableOnStop",
                false
            );
            Vector2 dir = ResolveDirection(
                merged,
                "throwDirection",
                root != null ? (Vector2)root.right : Vector2.right,
                root
            );

            return new List<(string key, object val)>
            {
                ("direction", dir.normalized),
                ("initialSpeed", Mathf.Max(0f, speed)),
                ("gravity", Mathf.Max(0f, gravity)),
                ("downwardSpeedClamp", Mathf.Max(0f, downwardClamp)),
                ("stopAfterSeconds", Mathf.Max(0f, stopAfter)),
                ("alignToVelocity", align),
                ("zeroVelocityOnStop", zeroOnStop),
                ("disableOnStop", disableOnStop),
                ("debugLogs", p.debugLogs || gen.debugLogs),
            };
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
    }
}
