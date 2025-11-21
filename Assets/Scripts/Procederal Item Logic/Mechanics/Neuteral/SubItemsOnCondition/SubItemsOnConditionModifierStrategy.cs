using System.Collections;
using System.Collections.Generic;
using Game.Procederal.Core.Builders.Modifiers;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Allows SubItemsOnCondition to be attached as a modifier so existing children can spawn sub-items.
    /// </summary>
    public class SubItemsOnConditionModifierStrategy : IModifierStrategy
    {
        public Game.Procederal.MechanicKind Kind =>
            Game.Procederal.MechanicKind.SubItemsOnCondition;

        public void Apply(
            Game.Procederal.ProcederalItemGenerator generator,
            GameObject target,
            Game.Procederal.ItemParams parameters
        )
        {
            bool parameterDebug =
                (parameters?.debugLogs ?? false)
                || (parameters?.subItemsOnConditionDebugLogs ?? false);
            bool generatorDebug =
                generator != null
                && (generator.debugLogs || generator.subItemsOnConditionDebugLogs);
            bool wantDebug = parameterDebug || generatorDebug;

            if (generator == null)
            {
                if (wantDebug)
                    Debug.LogWarning(
                        "[SubItemsOnConditionModifierStrategy] Generator was null; cannot apply modifier.",
                        target
                    );
                return;
            }

            if (target == null)
            {
                if (wantDebug)
                    Debug.LogWarning(
                        "[SubItemsOnConditionModifierStrategy] Target was null; skipping modifier application.",
                        generator
                    );
                return;
            }

            if (wantDebug)
            {
                int ruleCount = parameters?.spawnItemsOnConditions?.Count ?? 0;
                Debug.Log(
                    $"[SubItemsOnConditionModifierStrategy] Applying to '{target.name}' (rules={ruleCount}, debugLogs={wantDebug}).",
                    target
                );
            }

            var settingsDict = SubItemsOnConditionMechanicSettings.CreateDefaultSettings();
            SubItemsOnConditionMechanicSettings.ApplyParameterOverrides(
                settingsDict,
                parameters,
                includeRules: true,
                generatorDebug: generator.debugLogs || generator.subItemsOnConditionDebugLogs
            );

            var settings = new List<(string key, object val)>(settingsDict.Count);
            foreach (var kv in settingsDict)
                settings.Add((kv.Key, kv.Value));

            if (wantDebug)
            {
                int ruleCount = parameters?.spawnItemsOnConditions?.Count ?? 0;
                if (ruleCount > 0)
                {
                    Debug.Log(
                        $"[SubItemsOnConditionModifierStrategy] Prepared {ruleCount} spawn rule(s) for '{target.name}'.",
                        target
                    );
                }
                else
                {
                    Debug.Log(
                        "[SubItemsOnConditionModifierStrategy] No spawnItemsOnConditions entries supplied; mechanic will rely on existing configuration.",
                        target
                    );
                }
            }

            var added = generator.AddMechanicByName(
                target,
                "SubItemsOnCondition",
                settings.ToArray()
            );
            if (wantDebug)
            {
                if (added != null)
                {
                    Debug.Log(
                        $"[SubItemsOnConditionModifierStrategy] Added mechanic with {settings.Count} setting value(s) to '{target.name}'.",
                        target
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[SubItemsOnConditionModifierStrategy] Catalog lookup failed; falling back to direct component attachment.",
                        target
                    );
                }
            }

            var comp =
                added as SubItemsOnConditionMechanic
                ?? target.GetComponent<SubItemsOnConditionMechanic>();

            if (comp == null && added == null)
            {
                comp = target.AddComponent<SubItemsOnConditionMechanic>();

                if (wantDebug)
                {
                    Debug.Log(
                        $"[SubItemsOnConditionModifierStrategy] Fallback component added to '{target.name}'.",
                        comp
                    );
                }
            }

            if (comp == null)
            {
                if (wantDebug)
                {
                    Debug.LogWarning(
                        "[SubItemsOnConditionModifierStrategy] SubItemsOnConditionMechanic component not found after AddMechanicByName; ensure mechanic script is registered.",
                        target
                    );
                }
                return;
            }

            comp.SetGenerator(generator);
            comp.debugLogs |= wantDebug;

            SubItemsOnConditionMechanicSettings.Apply(comp, settingsDict);

            if (wantDebug)
            {
                int appliedRules = 0;
                if (
                    settingsDict.TryGetValue("spawnItemsOnCondition", out var rulesObj)
                    && rulesObj is IList list
                )
                {
                    appliedRules = list.Count;
                }

                Debug.Log(
                    $"[SubItemsOnConditionModifierStrategy] Component wiring complete; generator assigned, debugLogs enabled, and {appliedRules} rule(s) applied.",
                    comp
                );
            }
        }
    }
}
