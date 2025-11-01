using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class RippleBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Ripple;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var secondarySettings = gen.CollectSecondarySettings(instruction);
            var rippleJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Ripple"),
                secondarySettings
            );
            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                rippleJson
            );
            if (p != null)
            {
                movementMode =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.OverrideWithChildBehavior(
                        rippleJson,
                        movementMode,
                        p.childBehavior
                    );
            }
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );
            var ownerT = gen.ResolveOwner();
            float startRadius = MechanicSettingNormalizer.Radius(rippleJson, "startRadius", 1f);
            float endDiameter = MechanicSettingNormalizer.Float(
                rippleJson,
                "endDiameter",
                8f,
                0.01f
            );
            float growDuration = MechanicSettingNormalizer.Duration(
                rippleJson,
                "growDuration",
                1.5f
            );
            float edgeThickness = MechanicSettingNormalizer.Float(
                rippleJson,
                "edgeThickness",
                0.2f,
                0.01f
            );
            int damage = MechanicSettingNormalizer.Damage(rippleJson, "damage", 5);
            bool showViz = MechanicSettingNormalizer.Bool(rippleJson, "showVisualization", true);
            Color vizColor = MechanicSettingNormalizer.Color(rippleJson, "spriteColor", Color.cyan);
            bool spawnOnInterval = MechanicSettingNormalizer.Bool(
                rippleJson,
                "spawnOnInterval",
                false
            );
            int numberToSpawn = MechanicSettingNormalizer.Count(
                rippleJson,
                1,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn",
                "childrenToSpawn"
            );
            float interval = MechanicSettingNormalizer.Interval(
                rippleJson,
                0.5f,
                0.01f,
                "spawnInterval",
                "interval"
            );

            RippleIntervalSpawner spawner = null;
            bool wantsRepeating = spawnOnInterval || interval > 0.01f;
            if (wantsRepeating)
            {
                spawner =
                    root.GetComponent<RippleIntervalSpawner>()
                    ?? root.AddComponent<RippleIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = ownerT;
                spawner.interval = Mathf.Max(0.01f, interval);
                spawner.countPerInterval = Mathf.Max(1, numberToSpawn);
                spawner.startRadius = startRadius;
                spawner.endDiameter = endDiameter;
                spawner.growDuration = growDuration;
                spawner.edgeThickness = edgeThickness;
                spawner.damage = damage;
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.showVisualization = showViz;
                spawner.vizColor = vizColor;
                spawner.debugLogs = p.debugLogs || gen.debugLogs;
                spawner.parentSpawnedToSpawner = !shouldDetachChildren;

                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);

                // forward movementMode (if any) as modifier specs so interval spawns receive movement
                var movementSpecs =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMechanicSpecs(
                        rippleJson,
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
                                "Ripple",
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

            var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
            {
                new UnifiedChildBuilder.MechanicSpec
                {
                    Name = "Ripple",
                    Settings = new (string key, object val)[]
                    {
                        ("startRadius", startRadius),
                        ("endDiameter", endDiameter),
                        ("growDuration", growDuration),
                        ("edgeThickness", edgeThickness),
                        ("damage", damage),
                        ("excludeOwner", true),
                        ("requireMobTag", true),
                        ("showVisualization", showViz),
                        ("vizColor", vizColor),
                        ("debugLogs", p.debugLogs || gen.debugLogs),
                    },
                },
            };

            // Attach movement if the merged JSON requested it.
            Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                rippleJson,
                root.transform,
                p,
                gen,
                mechanics
            );

            var spec = new UnifiedChildBuilder.ChildSpec
            {
                ChildName = "Ripple",
                Parent = root.transform,
                Layer = root.layer,
                Mechanics = mechanics,
            };

            var ripple = UnifiedChildBuilder.BuildChild(gen, spec);
            if (shouldDetachChildren)
                ripple.transform.SetParent(null, worldPositionStays: true);
            subItems.Add(ripple);
        }
    }
}
