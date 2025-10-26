using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class WhipBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Whip;

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
            var whipJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Whip"),
                secondarySettings
            );
            int desired = MechanicSettingNormalizer.Count(
                whipJson,
                1,
                "childrenToSpawn",
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            int count = Mathf.Clamp(Mathf.Max(1, desired), 1, 4);
            string[] dirs = { "right", "up", "left", "down" };

            var set = root.AddComponent<WhipArcSet>();
            set.generator = gen;
            set.owner = ownerT;
            set.target = gen.target;
            set.debugLogs = p.debugLogs || gen.debugLogs;

            for (int i = 0; i < count; i++)
            {
                var settings = new List<(string key, object val)>();
                if (whipJson.TryGetValue("outerRadius", out var or))
                    settings.Add(("outerRadius", or));
                if (whipJson.TryGetValue("width", out var w))
                    settings.Add(("width", w));
                if (whipJson.TryGetValue("arcLengthDeg", out var ad))
                    settings.Add(("arcLengthDeg", ad));
                if (whipJson.TryGetValue("drawDuration", out var dd))
                    settings.Add(("drawDuration", dd));
                if (whipJson.TryGetValue("damageInterval", out var dmgInterval))
                    settings.Add(("interval", dmgInterval));
                else if (whipJson.TryGetValue("interval", out var iv))
                    settings.Add(("interval", iv));
                if (whipJson.TryGetValue("damagePerInterval", out var dmg))
                    settings.Add(("damagePerInterval", dmg));
                if (whipJson.TryGetValue("showVisualization", out var sv))
                    settings.Add(("showVisualization", sv));
                if (whipJson.TryGetValue("spriteColor", out var sc))
                    settings.Add(("vizColor", sc));

                settings.Add(("direction", dirs[i % dirs.Length]));
                settings.Add(("excludeOwner", true));
                settings.Add(("requireMobTag", true));
                settings.Add(("debugLogs", p.debugLogs || gen.debugLogs));

                var spec = new UnifiedChildBuilder.ChildSpec
                {
                    ChildName = $"Whip_{i}",
                    Parent = root.transform,
                    Layer = root.layer,
                    Mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                    {
                        new UnifiedChildBuilder.MechanicSpec
                        {
                            Name = "Whip",
                            Settings = settings.ToArray(),
                        },
                    },
                };

                var whip = UnifiedChildBuilder.BuildChild(gen, spec);
                subItems.Add(whip);
            }

            set.RefreshDirs();
        }
    }
}
