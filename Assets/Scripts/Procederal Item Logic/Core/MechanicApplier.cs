using System;
using System.Collections.Generic;
using Game.Procederal.Api;

namespace Game.Procederal.Core
{
    /// Centralizes mechanic type resolution and merged settings preparation.
    public static class MechanicApplier
    {
        public static bool TryPrepare(
            IMechanicCatalog catalog,
            MechanicSettingsCache cache,
            string mechanicName,
            IEnumerable<(string key, object value)> overrides,
            out Type mechanicType,
            out (string key, object value)[] finalSettings,
            out string failureReason
        )
        {
            mechanicType = null;
            finalSettings = Array.Empty<(string key, object value)>();
            failureReason = null;

            if (
                !TryResolveType(
                    catalog,
                    mechanicName,
                    out mechanicType,
                    out var mechanicPath,
                    out failureReason
                )
            )
            {
                return false;
            }

            var merged = ResolveSettingsDictionary(catalog, cache, mechanicName);
            if (overrides != null)
            {
                foreach (var (key, value) in overrides)
                {
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    merged[key] = value;
                }
            }

            finalSettings = ToArray(merged);
            return true;
        }

        public static bool TryResolveType(
            IMechanicCatalog catalog,
            string mechanicName,
            out Type mechanicType,
            out string mechanicPath,
            out string failureReason
        )
        {
            mechanicType = null;
            mechanicPath = null;
            failureReason = null;

            if (catalog == null)
            {
                failureReason = "No mechanic catalog available.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(mechanicName))
            {
                failureReason = "Mechanic name missing.";
                return false;
            }

            if (
                !catalog.TryGetPath(mechanicName, out mechanicPath)
                || string.IsNullOrWhiteSpace(mechanicPath)
            )
            {
                failureReason = $"Mechanic path not found for '{mechanicName}'.";
                return false;
            }

            mechanicType = MechanicReflection.ResolveTypeFromMechanicPath(mechanicPath);
            if (mechanicType == null)
            {
                failureReason =
                    $"Failed to resolve mechanic type for '{mechanicName}' from '{mechanicPath}'.";
                return false;
            }

            return true;
        }

        private static Dictionary<string, object> ResolveSettingsDictionary(
            IMechanicCatalog catalog,
            MechanicSettingsCache cache,
            string mechanicName
        )
        {
            if (cache != null && ReferenceEquals(cache.Catalog, catalog))
                return cache.Get(mechanicName);

            var dict = catalog?.GetMergedSettings(mechanicName);
            if (dict == null)
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (dict is Dictionary<string, object> dictionary)
                return new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);
            return new Dictionary<string, object>(dict, StringComparer.OrdinalIgnoreCase);
        }

        private static (string key, object value)[] ToArray(Dictionary<string, object> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<(string, object)>();

            var array = new (string key, object value)[source.Count];
            int index = 0;
            foreach (var kvp in source)
                array[index++] = (kvp.Key, kvp.Value);
            return array;
        }
    }
}
