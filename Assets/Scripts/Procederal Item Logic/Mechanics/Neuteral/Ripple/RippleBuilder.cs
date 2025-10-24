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
            float interval = MechanicSettingNormalizer.Interval(rippleJson, "interval", 0.5f);

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<RippleIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = ownerT;
                spawner.interval = interval;
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

                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);
                return;
            }

            var ripple = gen.CreateChild("Ripple", root.transform);
            gen.AddMechanicByName(
                ripple,
                "Ripple",
                new (string key, object val)[]
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
                }
            );
            gen.InitializeMechanics(ripple, gen.owner, gen.target);
            subItems.Add(ripple);
        }
    }
}
