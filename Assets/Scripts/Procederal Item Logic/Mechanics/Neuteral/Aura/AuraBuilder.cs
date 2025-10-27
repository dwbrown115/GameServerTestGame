using System.Collections.Generic;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class AuraBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind => Game.Procederal.MechanicKind.Aura;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            var auraJson = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("Aura"),
                gen.CollectSecondarySettings(instruction)
            );

            var movementMode = Game.Procederal.Core.Builders.BuilderMovementHelper.GetMovementMode(
                auraJson
            );
            bool shouldDetachChildren =
                Game.Procederal.Core.Builders.BuilderMovementHelper.ShouldDetachFromParent(
                    movementMode
                );

            float jRadius = MechanicSettingNormalizer.Radius(auraJson, "radius", 2f);
            float jInterval = MechanicSettingNormalizer.Interval(
                auraJson,
                0.5f,
                0.01f,
                "damageInterval",
                "interval"
            );
            int jDamage = MechanicSettingNormalizer.Damage(auraJson, "damagePerInterval", 1);
            bool jShowViz = MechanicSettingNormalizer.Bool(auraJson, "showVisualization", true);
            Color auraVizColor = MechanicSettingNormalizer.Color(
                auraJson,
                "spriteColor",
                new Color(0f, 0f, 0f, 0.5f)
            );

            var auraSettings = new List<(string key, object val)>
            {
                ("radius", jRadius),
                ("interval", Mathf.Max(0.01f, jInterval)),
                ("damagePerInterval", Mathf.Max(0, jDamage)),
                ("showVisualization", jShowViz),
                ("vizColor", auraVizColor),
                ("debugLogs", p.debugLogs || gen.debugLogs),
            };

            var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
            {
                new UnifiedChildBuilder.MechanicSpec
                {
                    Name = "Aura",
                    Settings = auraSettings.ToArray(),
                },
            };

            Game.Procederal.Core.Builders.BuilderMovementHelper.AttachMovementIfRequested(
                auraJson,
                root.transform,
                p,
                gen,
                mechanics
            );

            var spec = new UnifiedChildBuilder.ChildSpec
            {
                ChildName = "Aura",
                Parent = root.transform,
                Layer = root.layer,
                LocalScale = Vector3.one,
                Mechanics = mechanics,
            };

            var aura = UnifiedChildBuilder.BuildChild(gen, spec);
            if (shouldDetachChildren)
                aura.transform.SetParent(null, worldPositionStays: true);
            subItems.Add(aura);
        }
    }
}
