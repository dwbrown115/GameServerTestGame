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
            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                whipJson
            );
            if (p != null)
            {
                movementMode =
                    Game.Procederal.Core.Builders.BuilderMovementHelper.OverrideWithChildBehavior(
                        whipJson,
                        movementMode,
                        p.childBehavior
                    );
            }
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );
            int fallbackCount = BuilderChildCountHelper.ResolveFallbackCount(p, gen, 1);
            int desired = MechanicSettingNormalizer.Count(
                whipJson,
                fallbackCount,
                "childrenToSpawn",
                "numberOfItemsToSpawn",
                "NumberOfItemsToSpawn"
            );
            desired = BuilderChildCountHelper.ResolveFinalCount(desired, p, gen);
            int count = Mathf.Clamp(desired, 1, 4);
            string[] dirs = { "right", "up", "left", "down" };

            var set = root.AddComponent<WhipArcSet>();
            set.generator = gen;
            set.owner = ownerT;
            set.target = gen.ResolveTargetOrDefault();
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

                var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                {
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "Whip",
                        Settings = settings.ToArray(),
                    },
                };

                // Attach movement mode if requested by merged settings
                Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                    whipJson,
                    root.transform,
                    p,
                    gen,
                    mechanics
                );

                var spec = new UnifiedChildBuilder.ChildSpec
                {
                    ChildName = $"Whip_{i}",
                    Parent = root.transform,
                    Layer = root.layer,
                    Mechanics = mechanics,
                };

                var whip = UnifiedChildBuilder.BuildChild(gen, spec);
                if (shouldDetachChildren)
                {
                    Game.Procederal.ProcederalItemGenerator.DetachToWorld(
                        whip,
                        worldPositionStays: true
                    );
                }
                subItems.Add(whip);
            }

            set.RefreshDirs();
        }
    }
}
