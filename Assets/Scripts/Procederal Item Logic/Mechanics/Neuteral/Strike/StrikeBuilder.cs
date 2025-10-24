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
            var strike = gen.CreateChild("Strike", root.transform);
            var strikeJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Strike"),
                gen.CollectSecondarySettings(instruction)
            );
            Color vizColor = MechanicSettingNormalizer.Color(
                strikeJson,
                "spriteColor",
                Color.black
            );

            var settings = new List<(string key, object val)>();
            if (strikeJson.TryGetValue("interval", out var iv))
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

            gen.AddMechanicByName(strike, "Strike", settings.ToArray());
            gen.InitializeMechanics(strike, gen.owner, gen.target);
            subItems.Add(strike);
        }
    }
}
