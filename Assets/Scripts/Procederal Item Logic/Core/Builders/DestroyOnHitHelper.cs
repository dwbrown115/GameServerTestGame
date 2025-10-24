using System.Collections.Generic;
using Game.Procederal;

namespace Game.Procederal.Core.Builders
{
    public readonly struct DestroyOnHitConfig
    {
        public DestroyOnHitConfig(bool value, bool hasExplicitValue)
        {
            Value = value;
            HasExplicitValue = hasExplicitValue;
        }

        public bool Value { get; }
        public bool HasExplicitValue { get; }
    }

    public static class DestroyOnHitHelper
    {
        public static DestroyOnHitConfig Resolve(
            IDictionary<string, object> settings,
            ItemParams itemParams,
            bool fallback
        )
        {
            bool value = fallback;
            bool explicitValue = false;

            if (TryGetBool(settings, "destroyOnHit", out var lowerCase))
            {
                value = lowerCase;
                explicitValue = true;
            }
            else if (TryGetBool(settings, "DestroyOnHit", out var pascalCase))
            {
                value = pascalCase;
                explicitValue = true;
            }
            else if (TryGetBool(settings, "destroy_on_hit", out var snakeCase))
            {
                value = snakeCase;
                explicitValue = true;
            }

            if (!explicitValue && itemParams != null)
                value = itemParams.projectileDestroyOnHit;

            return new DestroyOnHitConfig(value, explicitValue);
        }

        private static bool TryGetBool(
            IDictionary<string, object> settings,
            string key,
            out bool value
        )
        {
            value = false;
            if (settings == null || string.IsNullOrWhiteSpace(key))
                return false;
            if (!settings.TryGetValue(key, out var raw))
                return false;
            return TryParseBool(raw, out value);
        }

        private static bool TryParseBool(object raw, out bool value)
        {
            switch (raw)
            {
                case bool b:
                    value = b;
                    return true;
                case string s when bool.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                case string s when int.TryParse(s, out var parsedInt):
                    value = parsedInt != 0;
                    return true;
                case int i:
                    value = i != 0;
                    return true;
                case long l:
                    value = l != 0;
                    return true;
                case float f:
                    value = !UnityEngine.Mathf.Approximately(f, 0f);
                    return true;
                case double d:
                    value = System.Math.Abs(d) > double.Epsilon;
                    return true;
                case decimal m:
                    value = m != 0m;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }
    }
}
