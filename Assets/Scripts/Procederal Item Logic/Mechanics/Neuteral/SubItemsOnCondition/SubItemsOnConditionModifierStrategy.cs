using System;
using System.Collections.Generic;
using Game.Procederal.Core.Builders.Modifiers;
using Newtonsoft.Json;
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
            bool parameterDebug = parameters?.debugLogs ?? false;
            bool generatorDebug = generator != null && generator.debugLogs;
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

            var settings = new List<(string key, object val)>();
            var overrideDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (parameters != null)
            {
                if (
                    parameters.spawnItemsOnConditions != null
                    && parameters.spawnItemsOnConditions.Count > 0
                )
                {
                    string json = JsonConvert.SerializeObject(parameters.spawnItemsOnConditions);
                    settings.Add(("spawnItemsOnCondition", json));

                    if (wantDebug)
                    {
                        Debug.Log(
                            $"[SubItemsOnConditionModifierStrategy] Serialized {parameters.spawnItemsOnConditions.Count} spawn rule(s) (payloadLength={json.Length}).",
                            target
                        );
                    }
                    overrideDict["spawnItemsOnCondition"] = json;
                }
                else if (wantDebug)
                {
                    Debug.Log(
                        "[SubItemsOnConditionModifierStrategy] ItemParams has no spawnItemsOnConditions entries; mechanic will rely on existing configuration.",
                        target
                    );
                }

                settings.Add(("debugLogs", parameters.debugLogs || generator.debugLogs));
                overrideDict["debugLogs"] = parameters.debugLogs || generator.debugLogs;
            }
            else
            {
                settings.Add(("debugLogs", generator.debugLogs));
                overrideDict["debugLogs"] = generator.debugLogs;
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

                var baseSettings = generator.LoadAndMergeJsonSettings("SubItemsOnCondition");
                var merged =
                    baseSettings != null
                        ? new Dictionary<string, object>(
                            baseSettings,
                            StringComparer.OrdinalIgnoreCase
                        )
                        : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in overrideDict)
                    merged[kv.Key] = kv.Value;

                SubItemsOnConditionMechanicSettings.Apply(comp, merged);

                if (wantDebug)
                {
                    Debug.Log(
                        $"[SubItemsOnConditionModifierStrategy] Applied {merged.Count} setting value(s) via fallback attachment to '{target.name}'.",
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

            if (wantDebug)
            {
                Debug.Log(
                    "[SubItemsOnConditionModifierStrategy] Component wiring complete; generator assigned and debugLogs enabled.",
                    comp
                );
            }
        }
    }
}
