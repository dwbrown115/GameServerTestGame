using System;
using System.Collections.Generic;
using Game.Procederal.Core.Builders.Modifiers;
using UnityEngine;

namespace Game.Procederal
{
    public partial class ProcederalItemGenerator
    {
        internal class SecondaryMechanicSettings
        {
            public string MechanicName;
            public Dictionary<string, object> Properties;
            public Dictionary<string, object> Overrides;
        }

        internal Transform ResolveOwner()
        {
            return owner != null ? owner : transform;
        }

        internal GameObject CreateChild(string name, Transform parent)
        {
            return AcquireObject(name, parent);
        }

        internal List<SecondaryMechanicSettings> CollectSecondarySettings(
            ItemInstruction instruction
        )
        {
            var list = new List<SecondaryMechanicSettings>();
            if (instruction == null || instruction.secondary == null)
                return list;

            foreach (var mechanicName in instruction.secondary)
            {
                if (string.IsNullOrWhiteSpace(mechanicName))
                    continue;

                var props = LoadAndMergeJsonSettings(mechanicName);
                var overrides = LoadKvpArrayForMechanic(mechanicName, "MechanicOverrides");
                list.Add(
                    new SecondaryMechanicSettings
                    {
                        MechanicName = mechanicName,
                        Properties = props,
                        Overrides = overrides,
                    }
                );
            }

            return list;
        }

        internal static Dictionary<string, object> CreateEffectiveSettings(
            Dictionary<string, object> baseSettings,
            List<SecondaryMechanicSettings> secondarySettings
        )
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var effective =
                baseSettings != null
                    ? new Dictionary<string, object>(baseSettings, comparer)
                    : new Dictionary<string, object>(comparer);

            void AddWithAlias(string key, object value)
            {
                if (string.IsNullOrEmpty(key))
                    return;
                effective[key] = value;
                if (char.IsUpper(key[0]))
                {
                    string camel = char.ToLowerInvariant(key[0]) + key.Substring(1);
                    effective[camel] = value;
                }
            }

            if (secondarySettings != null)
            {
                foreach (var entry in secondarySettings)
                {
                    if (entry == null)
                        continue;

                    if (entry.Properties != null)
                    {
                        foreach (var kv in entry.Properties)
                        {
                            AddWithAlias(kv.Key, kv.Value);
                        }
                    }

                    if (entry.Overrides != null)
                    {
                        foreach (var kv in entry.Overrides)
                        {
                            AddWithAlias(kv.Key, kv.Value);
                        }
                    }
                }
            }

            return effective;
        }

        public void AddModifierToAll(List<GameObject> subItems, MechanicKind kind, ItemParams p)
        {
            if (subItems == null)
                return;

            var strategy = ModifierStrategies.Get(kind);
            if (strategy == null)
            {
                Log($"No modifier strategy registered for '{kind}'.");
                return;
            }

            foreach (var go in subItems)
            {
                if (go == null)
                    continue;

                if (kind == MechanicKind.Orbit && HasMechanic(go, "Orbit"))
                {
                    Log($"Skipping duplicate '{kind}' on {go.name}.");
                    continue;
                }

                strategy.Apply(this, go, p);
                InitializeMechanics(go, owner, target);
            }
        }
    }
}
