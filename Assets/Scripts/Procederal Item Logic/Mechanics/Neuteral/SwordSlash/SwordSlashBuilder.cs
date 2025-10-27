using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class SwordSlashBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.SwordSlash;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = gen.ResolveOwner();
            var secondarySettings = gen.CollectSecondarySettings(instruction);
            var json = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("SwordSlash"),
                secondarySettings
            );
            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                json
            );
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );
            int seriesCount = Mathf.Max(1, MechanicSettingNormalizer.Int(json, "seriesCount", 3));
            float intervalBetween = MechanicSettingNormalizer.Float(
                json,
                "intervalBetween",
                0.08f,
                0f
            );
            bool spawnOnInterval = MechanicSettingNormalizer.Bool(json, "spawnOnInterval", false);
            float seriesInterval = MechanicSettingNormalizer.Interval(
                json,
                0.8f,
                0.01f,
                "spawnInterval",
                "interval"
            );
            float outerRadius = MechanicSettingNormalizer.Radius(json, "outerRadius", 1.5f);
            float width = MechanicSettingNormalizer.Float(json, "width", 0.5f, 0.0001f);
            float arcLen = MechanicSettingNormalizer.Float(json, "arcLengthDeg", 120f, 1f, 359f);
            bool edgeOnly = MechanicSettingNormalizer.Bool(json, "edgeOnly", true);
            float edgeThickness = MechanicSettingNormalizer.Float(
                json,
                "edgeThickness",
                0.2f,
                0.0001f
            );
            int damage = MechanicSettingNormalizer.Damage(json, "damage", 8);
            float speed = Mathf.Max(0.01f, MechanicSettingNormalizer.Speed(json, "speed", 12f));
            Color color = MechanicSettingNormalizer.Color(json, "spriteColor", Color.white);

            SwordSlashIntervalSpawner spawner = null;
            bool wantsRepeating = spawnOnInterval || seriesInterval > 0.01f;
            if (wantsRepeating)
            {
                spawner =
                    root.GetComponent<SwordSlashIntervalSpawner>()
                    ?? root.AddComponent<SwordSlashIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = ownerT;
                spawner.interval = Mathf.Max(0.01f, seriesInterval);
                spawner.seriesCount = Mathf.Max(1, seriesCount);
                spawner.intervalBetween = intervalBetween;
                spawner.outerRadius = Mathf.Max(0.0001f, outerRadius);
                spawner.width = Mathf.Max(0.0001f, width);
                spawner.arcLengthDeg = Mathf.Clamp(arcLen, 1f, 359f);
                spawner.edgeOnly = edgeOnly;
                spawner.edgeThickness = Mathf.Max(0.0001f, edgeThickness);
                spawner.vizColor = color;
                spawner.damage = Mathf.Max(0, damage);
                spawner.speed = Mathf.Max(0.01f, speed);
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.aimAtNearestEnemy = instruction.HasSecondary(
                    Game.Procederal.MechanicKind.Track
                );
                spawner.debugLogs = p.debugLogs || gen.debugLogs;
                spawner.parentSpawnedToSpawner = !shouldDetachChildren;

                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);

                // forward movementMode (if present) as modifier specs to the interval spawner
                var movementSpecs =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMechanicSpecs(
                        json,
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
                        if (
                            string.Equals(
                                ms.Name,
                                "SwordSlash",
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue;
                        spawner.AddModifierSpec(
                            ms.Name,
                            ms.Settings ?? System.Array.Empty<(string key, object val)>()
                        );
                    }
                }

                if (spawnOnInterval)
                    return;
            }

            Vector2 dir = Vector2.right;
            var ownerRb = ownerT != null ? ownerT.GetComponent<Rigidbody2D>() : null;
            if (ownerRb != null && ownerRb.linearVelocity.sqrMagnitude > 0.01f)
                dir = ownerRb.linearVelocity.normalized;

            if (instruction.HasSecondary(Game.Procederal.MechanicKind.Track) && ownerT != null)
            {
                if (
                    TargetingServiceLocator.Service.TryFindNearestMob(ownerT, out var nearest)
                    && nearest != null
                )
                {
                    Vector2 to = (Vector2)(nearest.position - ownerT.position);
                    if (to.sqrMagnitude > 1e-6f)
                        dir = to.normalized;
                }
            }

            int count = Mathf.Max(1, seriesCount);
            float spacing = Mathf.Max(0f, intervalBetween) * Mathf.Max(0.01f, speed);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos =
                    ownerT != null
                        ? ownerT.position - (Vector3)(dir * spacing * i)
                        : root.transform.position;
                var localPos = root.transform.InverseTransformPoint(spawnPos);

                var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                {
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "SwordSlash",
                        Settings = new (string key, object val)[]
                        {
                            ("outerRadius", outerRadius),
                            ("width", width),
                            ("arcLengthDeg", arcLen),
                            ("edgeOnly", edgeOnly),
                            ("edgeThickness", edgeThickness),
                            ("showVisualization", true),
                            ("vizColor", color),
                        },
                    },
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "Projectile",
                        Settings = new (string key, object val)[]
                        {
                            ("direction", dir),
                            ("speed", speed),
                            ("damage", damage),
                            ("requireMobTag", true),
                            ("excludeOwner", true),
                            ("destroyOnHit", true),
                            ("disableSelfSpeed", true),
                        },
                    },
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "ChildMovementMechanic",
                        Settings = new (string key, object val)[]
                        {
                            ("direction", dir),
                            ("speed", speed),
                            ("disableSelfSpeed", false),
                        },
                    },
                };

                // Allow 'movementMode' to append movement mechanics for the created slash child.
                Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                    json,
                    root.transform,
                    p,
                    gen,
                    mechanics
                );

                var spec = new UnifiedChildBuilder.ChildSpec
                {
                    ChildName = $"SwordSlash_{i}",
                    Parent = root.transform,
                    LocalPosition = localPos,
                    Layer = ownerT != null ? ownerT.gameObject.layer : root.layer,
                    Visual = new UnifiedChildBuilder.SpriteSpec { Enabled = false },
                    Rigidbody = new UnifiedChildBuilder.RigidbodySpec
                    {
                        Enabled = true,
                        BodyType = RigidbodyType2D.Dynamic,
                        FreezeRotation = true,
                        GravityScale = 0f,
                        Interpolation = RigidbodyInterpolation2D.Interpolate,
                        CollisionDetection = CollisionDetectionMode2D.Continuous,
                    },
                    Mechanics = mechanics,
                    LifetimeSeconds = 4f,
                    InitializeMechanics = false,
                };

                var slash = UnifiedChildBuilder.BuildChild(gen, spec);
                slash.transform.position = spawnPos;
                slash.transform.right = dir;

                gen.InitializeMechanics(slash, gen.owner, gen.target);
                if (shouldDetachChildren)
                    slash.transform.SetParent(null, worldPositionStays: true);

                var rb = slash.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = dir * speed;

                var sr = slash.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.enabled = false;

                subItems.Add(slash);
            }
        }
    }
}
