using System;
using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class ProjectileBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Projectile;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            if (gen == null || root == null)
                return;

            bool wantDebugLogs = (p != null && p.debugLogs) || gen.debugLogs;

            void DebugLog(string message)
            {
                if (wantDebugLogs)
                    Debug.Log($"[ProjectileBuilder] {message}", gen);
            }

            var secondarySettings = gen.CollectSecondarySettings(instruction);
            secondarySettings = SanitizeSecondarySettingsForProjectile(secondarySettings, DebugLog);
            var projectileJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Projectile"),
                secondarySettings
            );

            var destroyPolicy = DestroyOnHitHelper.Resolve(
                projectileJson,
                p,
                p != null ? p.projectileDestroyOnHit : true
            );
            bool destroyOnHit = destroyPolicy.Value;
            bool destroyExplicit = destroyPolicy.HasExplicitValue;

            if (!destroyExplicit && secondarySettings != null)
            {
                foreach (var entry in secondarySettings)
                {
                    if (entry == null)
                        continue;

                    if (entry.Properties != null && entry.Properties.Count > 0)
                    {
                        var propPolicy = DestroyOnHitHelper.Resolve(
                            entry.Properties,
                            null,
                            destroyOnHit
                        );
                        if (propPolicy.HasExplicitValue)
                        {
                            destroyOnHit = propPolicy.Value;
                            destroyExplicit = true;
                            break;
                        }
                    }

                    if (entry.Overrides != null && entry.Overrides.Count > 0)
                    {
                        var overridePolicy = DestroyOnHitHelper.Resolve(
                            entry.Overrides,
                            null,
                            destroyOnHit
                        );
                        if (overridePolicy.HasExplicitValue)
                        {
                            destroyOnHit = overridePolicy.Value;
                            destroyExplicit = true;
                            break;
                        }
                    }
                }
            }

            int fallbackCount = BuilderChildCountHelper.ResolveFallbackCount(p, gen, 1);
            int count = MechanicSettingNormalizer.Count(
                projectileJson,
                fallbackCount,
                "childrenToSpawn",
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            count = BuilderChildCountHelper.ResolveFinalCount(count, p, gen);

            DebugLog(
                destroyExplicit
                    ? $"Spawn count={count} with explicit destroyOnHit={destroyOnHit}"
                    : $"Spawn count={count} using fallback destroyOnHit={destroyOnHit}"
            );

            string spriteType = MechanicSettingNormalizer.String(
                projectileJson,
                "spriteType",
                "circle"
            );
            string customPath = MechanicSettingNormalizer.String(
                projectileJson,
                "customSpritePath",
                null
            );
            Color projColor = MechanicSettingNormalizer.Color(
                projectileJson,
                "spriteColor",
                Color.white
            );

            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                projectileJson
            );
            if (p != null)
            {
                movementMode =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.OverrideWithChildBehavior(
                        projectileJson,
                        movementMode,
                        p.childBehavior
                    );
            }

            var movementContext =
                Game.Procederal.Core.Builders.BuilderMovementHelper.BuildMovementContext(
                    projectileJson,
                    instruction
                );
            movementMode = movementContext.Mode;

            bool hasExplicitDirection = false;
            bool hasDirectionResolver = false;
            if (projectileJson != null)
            {
                foreach (var key in projectileJson.Keys)
                {
                    if (
                        !hasExplicitDirection
                        && string.Equals(key, "direction", StringComparison.OrdinalIgnoreCase)
                    )
                        hasExplicitDirection = true;
                    else if (
                        !hasDirectionResolver
                        && string.Equals(
                            key,
                            "directionFromResolver",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        hasDirectionResolver = true;

                    if (hasExplicitDirection && hasDirectionResolver)
                        break;
                }
            }

            bool useChildMovement = movementContext.UseChildMovement;
            bool movementRequiresDetachment = movementContext.DetachAfterInitialization;
            bool detachAfterInitialization = movementRequiresDetachment;
            bool disableProjectileSelfMovement = movementContext.DisableSelfMovement;

            Vector2 baseDirection = ResolveProjectileDirection(projectileJson, root.transform);

            bool shouldInjectDirection =
                !hasDirectionResolver
                && (
                    hasExplicitDirection
                    || useChildMovement
                    || movementMode
                        == Game.Procederal
                            .Core
                            .Builders
                            .BuilderMovementHelper
                            .MovementSelection
                            .Drop
                    || movementMode
                        == Game.Procederal
                            .Core
                            .Builders
                            .BuilderMovementHelper
                            .MovementSelection
                            .Throw
                );

            DebugLog(
                $"movementMode={movementMode} detachesAfterInit={detachAfterInitialization} pendingOrbit={movementContext.HasOrbitModifier}"
            );

            string spawnBehaviorRaw = MechanicSettingNormalizer.String(
                projectileJson,
                "spawnBehavior",
                null
            );
            string spawnBehavior = string.IsNullOrWhiteSpace(spawnBehaviorRaw)
                ? null
                : spawnBehaviorRaw.Trim().ToLowerInvariant();

            bool spawnOnInterval = MechanicSettingNormalizer.Bool(
                projectileJson,
                "spawnOnInterval",
                false
            );
            bool wantsInterval = spawnOnInterval;
            if (!wantsInterval && !string.IsNullOrEmpty(spawnBehavior))
            {
                if (string.Equals(spawnBehavior, "interval", StringComparison.OrdinalIgnoreCase))
                    wantsInterval = true;
            }
            int numberPerInterval = MechanicSettingNormalizer.Count(
                projectileJson,
                count,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn",
                "childrenToSpawn"
            );
            numberPerInterval = BuilderChildCountHelper.ResolveFinalCount(
                numberPerInterval,
                p,
                gen
            );
            float spawnInterval = MechanicSettingNormalizer.Interval(
                projectileJson,
                0.5f,
                0.01f,
                "spawnInterval",
                "interval"
            );
            float spawnRadius = MechanicSettingNormalizer.Radius(projectileJson, "spawnRadius", 0f);
            float lifetime = MechanicSettingNormalizer.Float(projectileJson, "lifetime", -1f);
            float projSpeed = MechanicSettingNormalizer.Float(projectileJson, "speed", -1f);
            float visualScale = MechanicSettingNormalizer.Float(
                projectileJson,
                "projectileSize",
                p != null ? p.projectileSize : 1f
            );
            visualScale = Mathf.Max(0.0001f, visualScale);

            bool excludeOwner = MechanicSettingNormalizer.Bool(
                projectileJson,
                "excludeOwner",
                true
            );
            bool requireMobTag = MechanicSettingNormalizer.Bool(
                projectileJson,
                "requireMobTag",
                true
            );

            if (wantsInterval)
            {
                var spawner = root.AddComponent<Game.Procederal.Api.GenericIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = gen.ResolveOwner();
                spawner.payloadMechanicName = "Projectile";
                spawner.interval = spawnInterval;
                spawner.countPerInterval = Mathf.Max(1, numberPerInterval);
                spawner.spawnRadius = Mathf.Max(0f, spawnRadius);
                spawner.lifetime = lifetime;
                spawner.spriteType = spriteType;
                spawner.customSpritePath = customPath;
                spawner.spriteColor = projColor;
                spawner.spawnScale = Mathf.Max(0.0001f, visualScale);
                spawner.createSpriteRenderer = true;
                spawner.createCollider = true;
                spawner.colliderRadius = 0.5f;
                spawner.createRigidBody = true;
                spawner.rigidBodyType = RigidbodyType2D.Kinematic;
                spawner.freezeRotation = true;
                spawner.parentSpawnedToSpawner = !movementRequiresDetachment;
                spawner.ignoreCollisionsWithOwner = excludeOwner;
                spawner.debugLogs = wantDebugLogs;
                bool shouldOverrideDestroy =
                    destroyExplicit || (p != null && p.projectileDestroyOnHit != true);
                spawner.autoDestroyPayloads = !(shouldOverrideDestroy && !destroyOnHit);

                var payloadSettings = new List<(string key, object val)>
                {
                    ("directionFromResolver", "vector2"),
                    ("excludeOwner", excludeOwner),
                    ("requireMobTag", requireMobTag),
                    ("debugLogs", wantDebugLogs),
                    ("disableSelfSpeed", disableProjectileSelfMovement),
                    ("destroyOnHit", destroyOnHit),
                    ("damage", p != null ? p.projectileDamage : 1),
                };
                if (projSpeed > 0f)
                    payloadSettings.Add(("speed", projSpeed));
                if (!string.IsNullOrEmpty(spriteType))
                    payloadSettings.Add(("spriteType", spriteType));
                if (!string.IsNullOrEmpty(customPath))
                    payloadSettings.Add(("customSpritePath", customPath));
                payloadSettings.Add(("spriteColor", projColor));

                spawner.SetPayloadSettings(payloadSettings.ToArray());

                if (!string.IsNullOrEmpty(spawnBehavior))
                {
                    if (spawnBehavior == "chaos")
                    {
                        var chaos =
                            root.GetComponent<Game.Procederal.Api.ChaosSpawnPosition>()
                            ?? root.AddComponent<Game.Procederal.Api.ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehavior == "neuteral" || spawnBehavior == "neutral")
                    {
                        var neutral =
                            root.GetComponent<Game.Procederal.Api.NeutralSpawnPositon>()
                            ?? root.AddComponent<Game.Procederal.Api.NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neutral;
                    }
                }
                else if (
                    movementMode
                    == Game.Procederal.Core.Builders.BuilderMovementHelper.MovementSelection.Throw
                )
                {
                    var chaos =
                        root.GetComponent<Game.Procederal.Api.ChaosSpawnPosition>()
                        ?? root.AddComponent<Game.Procederal.Api.ChaosSpawnPosition>();
                    spawner.spawnResolverBehaviour = chaos;
                }

                spawner.ClearModifierSpecs();

                if (useChildMovement)
                {
                    var moveSettings = new List<(string key, object val)>
                    {
                        ("directionFromResolver", "vector2"),
                        ("disableSelfSpeed", false),
                        ("debugLogs", wantDebugLogs),
                    };
                    if (projSpeed > 0f)
                        moveSettings.Add(("speed", projSpeed));
                    spawner.AddModifierSpec("ChildMovementMechanic", moveSettings.ToArray());
                }

                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);

                // Forward any configured movementMode as modifier specs so interval-spawned projectiles get the same movement.
                var movementSpecs =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMechanicSpecs(
                        projectileJson,
                        root.transform,
                        p,
                        gen
                    );
                if (movementSpecs != null && movementSpecs.Count > 0)
                {
                    foreach (var ms in movementSpecs)
                    {
                        if (string.IsNullOrWhiteSpace(ms.Name))
                            continue;
                        // Don't forward the primary 'Projectile' mechanic here
                        if (
                            string.Equals(
                                ms.Name,
                                "Projectile",
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue;
                        spawner.AddModifierSpec(
                            ms.Name,
                            ms.Settings ?? System.Array.Empty<(string key, object val)>()
                        );
                        DebugLog(
                            $"Forwarded movement mechanic spec '{ms.Name}' to interval spawner"
                        );
                    }
                }

                DebugLog(
                    $"Configured interval spawner parentSpawnedToSpawner={spawner.parentSpawnedToSpawner}"
                );

                return;
            }

            for (int i = 0; i < count; i++)
            {
                var projectileSettings = new List<(string key, object val)>
                {
                    ("damage", p != null ? p.projectileDamage : 1),
                    ("destroyOnHit", destroyOnHit),
                    ("excludeOwner", excludeOwner),
                    ("requireMobTag", requireMobTag),
                    ("debugLogs", wantDebugLogs),
                    ("disableSelfSpeed", disableProjectileSelfMovement),
                };
                if (shouldInjectDirection)
                    projectileSettings.Insert(0, ("direction", baseDirection));
                if (projSpeed > 0f)
                    projectileSettings.Add(("speed", projSpeed));
                if (!string.IsNullOrEmpty(spriteType))
                    projectileSettings.Add(("spriteType", spriteType));
                if (!string.IsNullOrEmpty(customPath))
                    projectileSettings.Add(("customSpritePath", customPath));
                projectileSettings.Add(("spriteColor", projColor));

                var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                {
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "Projectile",
                        Settings = projectileSettings.ToArray(),
                    },
                };

                if (useChildMovement)
                {
                    var childMovementSettings = BuildChildMovementSettings(
                        baseDirection,
                        projSpeed,
                        wantDebugLogs
                    );
                    mechanics.Add(
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "ChildMovementMechanic",
                            Settings = childMovementSettings.ToArray(),
                            SkipIfPresent = true,
                        }
                    );
                }

                // Allow movementMode to append Drop/Throw movement mechanics when requested.
                Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                    projectileJson,
                    root.transform,
                    p,
                    gen,
                    mechanics
                );

                List<Action<GameObject>> mutators = null;

                var spec = new UnifiedChildBuilder.ChildSpec
                {
                    ChildName = $"Projectile_{i}",
                    Parent = root.transform,
                    LocalScale = Vector3.one * visualScale,
                    Layer = root.layer,
                    Visual = new UnifiedChildBuilder.SpriteSpec
                    {
                        Enabled = true,
                        SpriteType = string.IsNullOrEmpty(spriteType) ? "circle" : spriteType,
                        CustomSpritePath = customPath,
                        Color = projColor,
                        SortingOrder = 0,
                    },
                    Collider = new UnifiedChildBuilder.ColliderSpec
                    {
                        Enabled = true,
                        Shape = UnifiedChildBuilder.ColliderShape2D.Circle,
                        Radius = 0.5f,
                        Offset = Vector2.zero,
                        IsTrigger = true,
                    },
                    Rigidbody = new UnifiedChildBuilder.RigidbodySpec
                    {
                        Enabled = true,
                        BodyType = RigidbodyType2D.Kinematic,
                        FreezeRotation = false,
                        GravityScale = 0f,
                        Interpolation = RigidbodyInterpolation2D.Interpolate,
                        CollisionDetection = CollisionDetectionMode2D.Continuous,
                    },
                    Mechanics = mechanics,
                    Mutators = mutators,
                    InitializeMechanics = false,
                };

                var go = UnifiedChildBuilder.BuildChild(gen, spec);
                gen.InitializeMechanics(go, gen.owner, gen.target);
                if (detachAfterInitialization)
                {
                    go.transform.SetParent(null, worldPositionStays: true);
                    DebugLog(
                        $"Detached projectile '{go.name}' from '{root.name}' (movementMode={movementMode})"
                    );
                }
                else if (wantDebugLogs)
                {
                    Debug.Log(
                        $"[ProjectileBuilder] Keeping projectile '{go.name}' parented to '{root.name}' (movementMode={movementMode})",
                        go
                    );
                }
                gen.SetExistingMechanicSetting(go, "Projectile", "destroyOnHit", destroyOnHit);
                subItems.Add(go);
            }

            if (gen.autoApplyCompatibleModifiers && subItems.Count > 0)
            {
                foreach (var mk in gen.GetModifiersToApply(instruction))
                    gen.AddModifierToAll(subItems, mk, p);
            }
        }

        private static Vector2 ResolveProjectileDirection(
            Dictionary<string, object> data,
            Transform root
        )
        {
            if (data != null && data.TryGetValue("direction", out var raw))
            {
                switch (raw)
                {
                    case Vector2 v when v.sqrMagnitude > 0.0001f:
                        return v.normalized;
                    case Vector3 v3 when v3.sqrMagnitude > 0.0001f:
                        return ((Vector2)v3).normalized;
                }
            }

            return root != null ? (Vector2)root.right : Vector2.right;
        }

        private static List<(string key, object val)> BuildChildMovementSettings(
            Vector2 direction,
            float speed,
            bool debugLogs
        )
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            var list = new List<(string key, object val)>
            {
                ("direction", dir),
                ("disableSelfSpeed", false),
                ("debugLogs", debugLogs),
            };

            if (speed > 0f)
                list.Add(("speed", speed));

            return list;
        }

        // Prevent modifier-provided orbit settings from clobbering projectile-specific values (e.g. collider radius).
        private static List<Game.Procederal.ProcederalItemGenerator.SecondaryMechanicSettings> SanitizeSecondarySettingsForProjectile(
            List<Game.Procederal.ProcederalItemGenerator.SecondaryMechanicSettings> original,
            Action<string> log
        )
        {
            if (original == null || original.Count == 0)
                return original;

            bool needsCleanup = false;
            foreach (var entry in original)
            {
                if (
                    entry?.Properties == null
                    || string.IsNullOrWhiteSpace(entry.MechanicName)
                    || !string.Equals(
                        entry.MechanicName,
                        "Orbit",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    continue;

                foreach (var key in entry.Properties.Keys)
                {
                    if (string.Equals(key, "radius", StringComparison.OrdinalIgnoreCase))
                    {
                        needsCleanup = true;
                        break;
                    }
                }

                if (needsCleanup)
                    break;
            }

            if (!needsCleanup)
                return original;

            log?.Invoke(
                "Orbit radius override detected in secondary settings; stripping to protect projectile collider radius."
            );

            var sanitized =
                new List<Game.Procederal.ProcederalItemGenerator.SecondaryMechanicSettings>(
                    original.Count
                );
            foreach (var entry in original)
            {
                if (entry == null)
                {
                    sanitized.Add(null);
                    continue;
                }

                if (
                    entry.Properties == null
                    || string.IsNullOrWhiteSpace(entry.MechanicName)
                    || !string.Equals(
                        entry.MechanicName,
                        "Orbit",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    sanitized.Add(entry);
                    continue;
                }

                var copy = new Dictionary<string, object>(
                    entry.Properties,
                    StringComparer.OrdinalIgnoreCase
                );
                object removedValue = null;
                if (entry.Properties.TryGetValue("radius", out var radiusRaw))
                    removedValue = radiusRaw;
                else if (entry.Properties.TryGetValue("Radius", out var radiusRawUpper))
                    removedValue = radiusRawUpper;

                copy.Remove("radius");
                copy.Remove("Radius");

                if (removedValue != null)
                {
                    log?.Invoke(
                        $"Removed Orbit.radius override (value={removedValue}) from secondary settings before building projectiles."
                    );
                }
                else
                {
                    log?.Invoke(
                        "Removed Orbit radius override with unspecified value from secondary settings before building projectiles."
                    );
                }

                sanitized.Add(
                    new Game.Procederal.ProcederalItemGenerator.SecondaryMechanicSettings
                    {
                        MechanicName = entry.MechanicName,
                        Properties = copy.Count > 0 ? copy : null,
                        Overrides = entry.Overrides,
                    }
                );
            }

            return sanitized;
        }
    }
}
