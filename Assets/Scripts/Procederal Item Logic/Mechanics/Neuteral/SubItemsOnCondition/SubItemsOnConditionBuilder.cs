using System;
using System.Collections.Generic;
using Mechanics.Neuteral;
using UnityEngine;

namespace Game.Procederal.Core.Builders
{
    public class SubItemsOnConditionBuilder : IPrimaryBuilder
    {
        public Game.Procederal.MechanicKind Kind =>
            Game.Procederal.MechanicKind.SubItemsOnCondition;

        public void Build(
            Game.Procederal.ProcederalItemGenerator gen,
            GameObject root,
            Game.Procederal.ItemInstruction instruction,
            Game.Procederal.ItemParams p,
            List<GameObject> subItems
        )
        {
            if (gen == null || root == null)
                return;

            var merged = Game.Procederal.ProcederalItemGenerator.CreateEffectiveSettings(
                gen.LoadAndMergeJsonSettings("SubItemsOnCondition"),
                gen.CollectSecondarySettings(instruction)
            );

            int fallbackCount = BuilderChildCountHelper.ResolveFallbackCount(p, gen, 1);
            int count = MechanicSettingNormalizer.Count(
                merged,
                fallbackCount,
                "listeners",
                "count",
                "spawnCount"
            );
            count = BuilderChildCountHelper.ResolveFinalCount(count, p, gen);

            float triggerRadius = MechanicSettingNormalizer.Radius(merged, "triggerRadius", 0.75f);

            var mechanicSettings = BuildMechanicSettings(merged, gen, p);

            for (int i = 0; i < count; i++)
            {
                var mechanics = new List<UnifiedChildBuilder.MechanicSpec>
                {
                    new UnifiedChildBuilder.MechanicSpec
                    {
                        Name = "SubItemsOnCondition",
                        Settings = mechanicSettings,
                    },
                };

                var spec = new UnifiedChildBuilder.ChildSpec
                {
                    ChildName = count > 1 ? $"SubItemsOnCondition_{i}" : "SubItemsOnCondition",
                    Parent = root.transform,
                    Layer = root.layer,
                    Mechanics = mechanics,
                    Collider = new UnifiedChildBuilder.ColliderSpec
                    {
                        Enabled = true,
                        Shape = UnifiedChildBuilder.ColliderShape2D.Circle,
                        Radius = Mathf.Max(0.05f, triggerRadius),
                        Offset = Vector2.zero,
                        IsTrigger = true,
                    },
                    Rigidbody = new UnifiedChildBuilder.RigidbodySpec { Enabled = false },
                    Visual = new UnifiedChildBuilder.SpriteSpec { Enabled = false },
                    Mutators = new List<Action<GameObject>>
                    {
                        go =>
                        {
                            var comp = go.GetComponent<SubItemsOnConditionMechanic>();
                            if (comp != null)
                            {
                                comp.SetGenerator(gen);
                                comp.debugLogs |= (p != null && p.debugLogs) || gen.debugLogs;
                                if (
                                    p != null
                                    && p.spawnItemsOnConditions != null
                                    && p.spawnItemsOnConditions.Count > 0
                                )
                                {
                                    comp.ClearRules();
                                    foreach (var specEntry in p.spawnItemsOnConditions)
                                    {
                                        var rule = SubItemsOnConditionMechanic.CreateRuleFromSpec(
                                            specEntry
                                        );
                                        if (rule != null)
                                            comp.AddRule(rule);
                                    }
                                }
                            }
                        },
                    },
                };

                var child = UnifiedChildBuilder.BuildChild(gen, spec);
                subItems.Add(child);
            }
        }

        private static (string key, object val)[] BuildMechanicSettings(
            Dictionary<string, object> merged,
            Game.Procederal.ProcederalItemGenerator gen,
            Game.Procederal.ItemParams p
        )
        {
            var list = new List<(string key, object val)>();
            if (merged != null)
            {
                foreach (var kv in merged)
                {
                    if (kv.Key == null)
                        continue;
                    list.Add((kv.Key, kv.Value));
                }
            }

            bool debug = (p != null && p.debugLogs) || (gen != null && gen.debugLogs);
            list.Add(("debugLogs", debug));

            return list.ToArray();
        }
    }
}
