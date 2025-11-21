using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Procederal;
using Newtonsoft.Json;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Applies dictionary-based settings to <see cref="SubItemsOnConditionMechanic"/> when loaded from JSON.
    /// </summary>
    public static class SubItemsOnConditionMechanicSettings
    {
        private static readonly string[] SpecKeys =
        {
            "spawnitemsoncondition",
            "SpawnItemsOnCondition",
            "conditionSpec",
            "spec",
            "Spec",
            "json",
        };

        public const int DefaultListenerCount = 1;
        public const float DefaultTriggerRadius = 0.75f;

        public static Dictionary<string, object> CreateDefaultSettings()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["listeners"] = DefaultListenerCount,
                ["triggerRadius"] = DefaultTriggerRadius,
                ["debugLogs"] = false,
                ["logRuleEvaluations"] = false,
                ["logRuleSkips"] = false,
            };
        }

        public static Dictionary<string, object> CreateSettingsFromParameters(
            ItemParams parameters,
            bool includeRules = true,
            bool generatorDebug = false
        )
        {
            var dict = CreateDefaultSettings();
            ApplyParameterOverrides(dict, parameters, includeRules, generatorDebug);
            return dict;
        }

        public static void ApplyParameterOverrides(
            Dictionary<string, object> settings,
            ItemParams parameters,
            bool includeRules = true,
            bool generatorDebug = false
        )
        {
            if (settings == null)
                return;

            int listeners =
                parameters != null
                    ? Mathf.Max(1, parameters.subItemsOnConditionListeners)
                    : DefaultListenerCount;
            settings["listeners"] = listeners;

            float radius =
                parameters != null
                    ? Mathf.Max(0.05f, parameters.subItemsOnConditionTriggerRadius)
                    : DefaultTriggerRadius;
            settings["triggerRadius"] = radius;

            bool debug = generatorDebug;
            if (parameters != null)
            {
                debug = debug || parameters.debugLogs || parameters.subItemsOnConditionDebugLogs;
            }
            settings["debugLogs"] = debug;

            settings["logRuleEvaluations"] =
                parameters?.subItemsOnConditionLogRuleEvaluations ?? false;
            settings["logRuleSkips"] = parameters?.subItemsOnConditionLogRuleSkips ?? false;

            if (includeRules)
            {
                if (parameters?.spawnItemsOnConditions != null)
                {
                    if (parameters.spawnItemsOnConditions.Count > 0)
                        settings["spawnItemsOnCondition"] = parameters.spawnItemsOnConditions;
                    else
                        settings.Remove("spawnItemsOnCondition");
                }
                else
                {
                    settings.Remove("spawnItemsOnCondition");
                }
            }
        }

        public static void Apply(
            SubItemsOnConditionMechanic comp,
            IDictionary<string, object> settings
        )
        {
            if (comp == null || settings == null)
                return;

            var specs = ExtractSpecs(settings);
            if (comp.debugLogs)
            {
                Debug.Log(
                    $"[SubItemsOnCondition] Settings.Apply -> keys={settings.Count}, extractedRules={specs.Count}",
                    comp
                );
            }

            if (specs.Count == 0)
            {
                if (comp.debugLogs)
                {
                    Debug.Log(
                        "[SubItemsOnCondition] Settings.Apply -> No specs extracted; rules unchanged.",
                        comp
                    );
                }
                return;
            }

            comp.ClearRules();

            bool wantDebug = comp.debugLogs;
            foreach (var spec in specs)
            {
                var rule = SubItemsOnConditionMechanic.CreateRuleFromSpec(spec);
                if (rule == null)
                    continue;
                comp.AddRule(rule);
                wantDebug |= rule.debugLogs;
                if (comp.debugLogs)
                {
                    Debug.Log(
                        $"[SubItemsOnCondition] Settings.Apply -> Added rule primary={rule.primary}, condition={rule.condition}, spawnCount={rule.spawnCount}, secondary={rule.secondary?.Count ?? 0}",
                        comp
                    );
                }
            }

            comp.debugLogs = wantDebug;
        }

        private static List<ItemParams.SpawnItemsOnConditionSpec> ExtractSpecs(
            IDictionary<string, object> settings
        )
        {
            var result = new List<ItemParams.SpawnItemsOnConditionSpec>();
            if (settings == null)
                return result;

            var processed = new HashSet<object>();
            foreach (var key in SpecKeys)
            {
                if (!settings.TryGetValue(key, out var raw) || raw == null)
                    continue;

                if (!processed.Add(raw))
                    continue;

                AppendSpecsFromRaw(raw, result);
            }

            if (result.Count == 0)
                AppendSpecsFromRaw(settings, result);

            return result;
        }

        private static void AppendSpecsFromRaw(
            object raw,
            List<ItemParams.SpawnItemsOnConditionSpec> destination
        )
        {
            if (raw == null || destination == null)
                return;

            switch (raw)
            {
                case string s:
                    AppendSpecsFromJsonString(s, destination);
                    break;
                case IDictionary dict:
                    AppendSpecsFromJsonString(JsonConvert.SerializeObject(dict), destination);
                    break;
                case IEnumerable enumerable:
                    foreach (var entry in enumerable)
                        AppendSpecsFromRaw(entry, destination);
                    break;
                default:
                    AppendSpecsFromJsonString(JsonConvert.SerializeObject(raw), destination);
                    break;
            }
        }

        private static void AppendSpecsFromJsonString(
            string raw,
            List<ItemParams.SpawnItemsOnConditionSpec> destination
        )
        {
            if (string.IsNullOrWhiteSpace(raw) || destination == null)
                return;

            string json = TrimToJsonPayload(raw);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                if (json.StartsWith("{"))
                {
                    var spec = JsonConvert.DeserializeObject<ItemParams.SpawnItemsOnConditionSpec>(
                        json
                    );
                    NormalizeSpec(spec, destination);
                }
                else if (json.StartsWith("["))
                {
                    var specs = JsonConvert.DeserializeObject<
                        List<ItemParams.SpawnItemsOnConditionSpec>
                    >(json);
                    if (specs != null)
                    {
                        foreach (var spec in specs)
                            NormalizeSpec(spec, destination);
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.LogWarning(
                    $"[SubItemsOnConditionMechanicSettings] Failed to parse spec JSON: {ex.Message}\nInput: {json}"
                );
            }
        }

        private static string TrimToJsonPayload(string raw)
        {
            string trimmed = raw.Trim();
            int objIndex = trimmed.IndexOf('{');
            int arrIndex = trimmed.IndexOf('[');

            int startIndex;
            if (objIndex >= 0 && arrIndex >= 0)
                startIndex = Mathf.Min(objIndex, arrIndex);
            else if (objIndex >= 0)
                startIndex = objIndex;
            else
                startIndex = arrIndex;

            if (startIndex > 0)
                trimmed = trimmed.Substring(startIndex).Trim();

            return trimmed;
        }

        private static void NormalizeSpec(
            ItemParams.SpawnItemsOnConditionSpec spec,
            List<ItemParams.SpawnItemsOnConditionSpec> destination
        )
        {
            if (spec == null || destination == null)
                return;

            if (string.IsNullOrWhiteSpace(spec.primary))
                spec.primary = "Projectile";
            if (string.IsNullOrWhiteSpace(spec.condition))
                spec.condition = "mobContact";
            if (string.IsNullOrWhiteSpace(spec.target))
                spec.target = "mob";
            else
                spec.target = spec.target.Trim();
            spec.spawnCount = Mathf.Max(1, spec.spawnCount);
            spec.cooldownSeconds = Mathf.Max(0f, spec.cooldownSeconds);
            spec.secondary ??= new List<string>();
            for (int i = spec.secondary.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(spec.secondary[i]))
                    spec.secondary.RemoveAt(i);
                else
                    spec.secondary[i] = spec.secondary[i].Trim();
            }

            destination.Add(spec);
        }
    }
}
