using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class BeamBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Beam;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var ownerT = gen.ResolveOwner();
            var beamJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Beam"),
                gen.CollectSecondarySettings(instruction)
            );

            Color vizColor = MechanicSettingNormalizer.Color(beamJson, "spriteColor", Color.white);

            var beamSettings = new List<(string key, object val)>();
            if (beamJson.TryGetValue("maxDistance", out var md))
                beamSettings.Add(("maxDistance", md));
            if (beamJson.TryGetValue("speed", out var sp))
                beamSettings.Add(("speed", sp));
            if (beamJson.TryGetValue("extendSpeed", out var es))
                beamSettings.Add(("extendSpeed", es));
            if (beamJson.TryGetValue("direction", out var dir))
                beamSettings.Add(("direction", dir));
            if (beamJson.TryGetValue("radius", out var br))
                beamSettings.Add(("radius", br));
            if (beamJson.TryGetValue("beamWidth", out var bw))
                beamSettings.Add(("beamWidth", bw));
            if (beamJson.TryGetValue("damagePerInterval", out var dpi))
                beamSettings.Add(("damagePerInterval", dpi));
            if (beamJson.TryGetValue("damage", out var dmg))
                beamSettings.Add(("damage", dmg));
            if (beamJson.TryGetValue("damageInterval", out var dmgInterval))
                beamSettings.Add(("interval", dmgInterval));
            else if (beamJson.TryGetValue("interval", out var biv))
                beamSettings.Add(("interval", biv));
            beamSettings.Add(("requireMobTag", true));
            beamSettings.Add(("excludeOwner", true));
            beamSettings.Add(("showVisualization", true));
            beamSettings.Add(("vizColor", vizColor));
            beamSettings.Add(("debugLogs", p.debugLogs || gen.debugLogs));

            bool spawnOnInterval = MechanicSettingNormalizer.Bool(
                beamJson,
                "spawnOnInterval",
                false
            );
            int numberToSpawn = MechanicSettingNormalizer.Count(
                beamJson,
                1,
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn",
                "childrenToSpawn"
            );
            float spawnInterval = MechanicSettingNormalizer.Interval(
                beamJson,
                0.5f,
                0.01f,
                "spawnInterval",
                "interval"
            );

            if (gen.debugLogs)
            {
                Debug.Log(
                    $"[ProcederalItemGenerator] Beam spawnInterval resolved to {spawnInterval}",
                    gen
                );
            }

            string spawnBehavior = null;
            if (beamJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();
            float spawnRadius = MechanicSettingNormalizer.Radius(beamJson, "spawnRadius", 0f);

            BeamIntervalSpawner spawner = null;
            bool wantsRepeating =
                spawnOnInterval
                || spawnInterval > 0.01f
                || string.Equals(spawnBehaviorNorm, "interval");
            if (wantsRepeating)
            {
                spawner =
                    root.GetComponent<BeamIntervalSpawner>()
                    ?? root.AddComponent<BeamIntervalSpawner>();
                spawner.generator = gen;
                spawner.owner = ownerT;
                spawner.interval = Mathf.Max(0.01f, spawnInterval);
                spawner.countPerInterval = Mathf.Max(1, numberToSpawn);
                spawner.debugLogs = p.debugLogs || gen.debugLogs;

                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<ChaosSpawnPosition>()
                            ?? root.AddComponent<ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<NeutralSpawnPositon>()
                            ?? root.AddComponent<NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                }

                spawner.SetBeamSettings(beamSettings.ToArray());
                spawner.spawnRadius = spawnRadius;
                if (gen.autoApplyCompatibleModifiers)
                    gen.ForwardModifiersToSpawner(spawner, instruction, p);

                if (spawnOnInterval || string.Equals(spawnBehaviorNorm, "interval"))
                    return;
            }

            var spec = new UnifiedChildBuilder.ChildSpec
            {
                ChildName = "Beam",
                Parent = root.transform,
                Layer = root.layer,
                Mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                {
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "Beam",
                        Settings = beamSettings.ToArray(),
                    },
                },
            };

            var beam = UnifiedChildBuilder.BuildChild(gen, spec);
            subItems.Add(beam);
        }
    }
}
