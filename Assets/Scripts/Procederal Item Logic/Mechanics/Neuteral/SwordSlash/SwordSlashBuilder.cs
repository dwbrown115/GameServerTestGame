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
            int seriesCount = Mathf.Max(1, MechanicSettingNormalizer.Int(json, "seriesCount", 3));
            float intervalBetween = MechanicSettingNormalizer.Float(
                json,
                "intervalBetween",
                0.08f,
                0f
            );
            bool spawnOnInterval = MechanicSettingNormalizer.Bool(json, "spawnOnInterval", false);
            float seriesInterval = MechanicSettingNormalizer.Interval(json, "interval", 0.8f);
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

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<SwordSlashIntervalSpawner>();
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

                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);
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
                var shell = new SpawnHelpers.PayloadShellOptions
                {
                    parent = root.transform,
                    position = spawnPos,
                    layer = ownerT != null ? ownerT.gameObject.layer : root.layer,
                    spriteType = null,
                    customSpritePath = null,
                    spriteColor = color,
                    createCollider = false,
                    colliderRadius = 0.5f,
                    createRigidBody = true,
                    bodyType = RigidbodyType2D.Dynamic,
                    freezeRotation = true,
                    addAutoDestroy = true,
                    lifetimeSeconds = 4f,
                };

                var slash = SpawnHelpers.CreatePayloadShell($"SwordSlash_{i}", shell);
                if (slash.transform.parent != root.transform)
                    slash.transform.SetParent(root.transform, worldPositionStays: true);

                gen.AddMechanicByName(
                    slash,
                    "SwordSlash",
                    new (string key, object val)[]
                    {
                        ("outerRadius", outerRadius),
                        ("width", width),
                        ("arcLengthDeg", arcLen),
                        ("edgeOnly", edgeOnly),
                        ("edgeThickness", edgeThickness),
                        ("showVisualization", true),
                        ("vizColor", color),
                    }
                );

                slash.transform.right = dir;

                gen.AddMechanicByName(
                    slash,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("direction", dir),
                        ("speed", speed),
                        ("damage", damage),
                        ("requireMobTag", true),
                        ("excludeOwner", true),
                        ("destroyOnHit", true),
                        ("disableSelfSpeed", true),
                    }
                );

                gen.AddMechanicByName(
                    slash,
                    "ChildMovementMechanic",
                    new (string key, object val)[]
                    {
                        ("direction", dir),
                        ("speed", speed),
                        ("disableSelfSpeed", false),
                    }
                );

                var rb = slash.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = dir * speed;

                gen.InitializeMechanics(slash, gen.owner, gen.target);
                subItems.Add(slash);
            }
        }
    }
}
