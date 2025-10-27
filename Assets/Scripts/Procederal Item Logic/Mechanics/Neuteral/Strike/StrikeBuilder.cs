using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class StrikeBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Strike;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var strikeJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Strike"),
                gen.CollectSecondarySettings(instruction)
            );
            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                strikeJson
            );
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );
            Color vizColor = MechanicSettingNormalizer.Color(
                strikeJson,
                "spriteColor",
                Color.black
            );

            var settings = new List<(string key, object val)>();
            if (strikeJson.TryGetValue("damageInterval", out var dmgInterval))
                settings.Add(("interval", dmgInterval));
            else if (strikeJson.TryGetValue("interval", out var iv))
                settings.Add(("interval", iv));
            if (strikeJson.TryGetValue("damagePerInterval", out var dmg))
                settings.Add(("damagePerInterval", dmg));
            if (strikeJson.TryGetValue("damage", out var dmg2))
                settings.Add(("damage", dmg2));
            settings.Add(("requireMobTag", true));
            settings.Add(("excludeOwner", true));
            settings.Add(("showVisualization", true));
            settings.Add(("vizColor", vizColor));
            settings.Add(("debugLogs", p.debugLogs || gen.debugLogs));

            var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
            {
                new UnifiedChildBuilder.MechanicSpec
                {
                    Name = "Strike",
                    Settings = settings.ToArray(),
                },
            };

            Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                strikeJson,
                root.transform,
                p,
                gen,
                mechanics
            );

            var spec = new UnifiedChildBuilder.ChildSpec
            {
                ChildName = "Strike",
                Parent = root.transform,
                Layer = root.layer,
                Mechanics = mechanics,
            };

            var strike = UnifiedChildBuilder.BuildChild(gen, spec);
            if (shouldDetachChildren)
                strike.transform.SetParent(null, worldPositionStays: true);
            subItems.Add(strike);
        }
    }
}
