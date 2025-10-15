using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Procederal;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Offline helper that reads Primary/Modifier JSON lists and returns a random, compatible
    /// primary + single modifier combination with in-memory overrides applied onto ItemParams.
    /// Does NOT mutate JSON files.
    public static class OfflineItemGeneratorApi
    {
        private const string PrimaryResourceFolder = "ProcederalMechanics/Primary";
        private const string ModifierResourceFolder = "ProcederalMechanics/Modifier";

        [Serializable]
        private class PrimaryEntry
        {
            public string MechanicName;
            public string MechanicPath;
            public List<SerializableKvp> Properties = new List<SerializableKvp>();
            public List<string> IncompatibleWith = new List<string>();

            // Unity-friendly explicit fields (optional)
            public int DefaultDamage;
            public float DefaultRadius;
            public float DefaultInterval;
            public bool DestroyOnHitDefault;
        }

        [Serializable]
        private class ModifierEntry
        {
            public string MechanicName;
            public string MechanicPath;
            public List<SerializableKvp> MechanicOverrides = new List<SerializableKvp>();
            public List<string> IncompatibleWith = new List<string>();

            // Unity-friendly explicit fields (optional)
            public bool DestroyOnHitOverride;
            public float OrbitRadius;
            public float OrbitSpeedDeg;
            public float DrainRadius;
            public float DrainInterval;
            public int DrainDamage;
            public float LifeStealRatio;
        }

        [Serializable]
        private class SerializableKvp
        {
            public string Key;
            public string Value;
        }

        [Serializable]
        private class PrimaryListWrapper
        {
            public List<PrimaryEntry> Items = new();
        }

        [Serializable]
        private class ModifierListWrapper
        {
            public List<ModifierEntry> Items = new();
        }

        // Minimal adapter so we can parse the simple array-style JSON you've got
        private static List<T> FromJsonArray<T>(string json)
        {
            // Unity can't directly parse top-level arrays; wrap it
            string wrapped = "{\"Items\":" + json + "}";
            return JsonUtility.FromJson<Wrapper<T>>(wrapped)?.Items ?? new List<T>();
        }

        [Serializable]
        private class Wrapper<T>
        {
            public List<T> Items = new();
        }

        public struct GeneratedCombo
        {
            public ItemInstruction instruction; // primary + [modifier]
            public ItemParams parameters; // in-memory overrides applied here
            public string debug; // selection details
        }

        public static GeneratedCombo MakeRandom(
            TextAsset primaryJson,
            TextAsset modifierJson,
            System.Random rng = null
        )
        {
            string primaryText = LoadCatalog(primaryJson, PrimaryResourceFolder);
            string modifierText = LoadCatalog(modifierJson, ModifierResourceFolder);

            if (string.IsNullOrWhiteSpace(primaryText) || primaryText.Trim() == "[]")
                throw new ArgumentException("Primary JSON is null/empty");
            if (string.IsNullOrWhiteSpace(modifierText) || modifierText.Trim() == "[]")
                throw new ArgumentException("Modifier JSON is null/empty");

            rng ??= new System.Random();

            var primaries = FromJsonArray<PrimaryEntry>(primaryText);
            var modifiers = FromJsonArray<ModifierEntry>(modifierText);
            if (primaries.Count == 0)
                throw new InvalidOperationException("No primary mechanics defined");

            // Pick a random primary
            var primary = primaries[rng.Next(primaries.Count)];
            var primaryKind = Game.Procederal.ItemInstruction.ParseKind(primary.MechanicName);

            // Filter modifiers compatible with this primary
            var compatibleMods = modifiers.Where(m => IsCompatible(primary, m)).ToList();

            ModifierEntry chosenMod = null;
            if (compatibleMods.Count > 0)
                chosenMod = compatibleMods[rng.Next(compatibleMods.Count)];

            // Build instruction and params
            var instruction = new ItemInstruction
            {
                primary = primary.MechanicName,
                secondary = new List<string>(),
            };
            if (chosenMod != null)
                instruction.secondary.Add(chosenMod.MechanicName);

            var p = new ItemParams();
            // Seed params with reasonable defaults derived from primary properties if present
            ApplyPrimaryPropertiesToParams(primary, ref p);
            // Apply modifier overrides onto p (in-memory only)
            if (chosenMod != null)
                ApplyModifierOverridesToParams(chosenMod, primaryKind, ref p);

            var dbg =
                $"Primary={primary.MechanicName} Mod={(chosenMod != null ? chosenMod.MechanicName : "<none>")}";
            return new GeneratedCombo
            {
                instruction = instruction,
                parameters = p,
                debug = dbg,
            };
        }

        private static bool IsCompatible(PrimaryEntry primary, ModifierEntry mod)
        {
            if (primary == null || mod == null)
                return false;
            if (
                primary.IncompatibleWith != null
                && primary.IncompatibleWith.Contains(mod.MechanicName)
            )
                return false;
            if (mod.IncompatibleWith != null && mod.IncompatibleWith.Contains(primary.MechanicName))
                return false;
            // Specific early rule example: Aura incompatible with Orbit (already in JSON primary side)
            return true;
        }

        private static void ApplyPrimaryPropertiesToParams(PrimaryEntry primary, ref ItemParams p)
        {
            if (primary?.Properties == null)
                return;
            foreach (var kv in primary.Properties)
            {
                if (kv == null || string.IsNullOrEmpty(kv.Key))
                    continue;
                // Defaults mapping (extendable):
                switch (kv.Key)
                {
                    case "AllowMultiple":
                        // Not a param; affects generator logic elsewhere; ignore here
                        break;
                    case "DestroyOnHit":
                        // For Projectile primary
                        bool destroy = ParseBool(kv.Value, true);
                        p.projectileDestroyOnHit = destroy;
                        break;
                    // Future extendables, e.g. default damage/radius if present
                    case "DefaultDamage":
                        p.projectileDamage = ParseInt(kv.Value, p.projectileDamage);
                        p.auraDamage = ParseInt(kv.Value, p.auraDamage);
                        break;
                    case "DefaultRadius":
                        p.auraRadius = ParseFloat(kv.Value, p.auraRadius);
                        break;
                    case "DefaultInterval":
                        p.auraInterval = ParseFloat(kv.Value, p.auraInterval);
                        break;
                }
            }

            // Unity-friendly explicit fields from primary (take precedence if present)
            if (primary.DefaultDamage != 0)
            {
                p.projectileDamage = primary.DefaultDamage;
                p.auraDamage = primary.DefaultDamage;
            }
            if (primary.DefaultRadius > 0f)
            {
                p.auraRadius = primary.DefaultRadius;
            }
            if (primary.DefaultInterval > 0f)
            {
                p.auraInterval = primary.DefaultInterval;
            }
            // DestroyOnHit default for projectile
            p.projectileDestroyOnHit = p.projectileDestroyOnHit || primary.DestroyOnHitDefault;
        }

        private static void ApplyModifierOverridesToParams(
            ModifierEntry mod,
            MechanicKind primaryKind,
            ref ItemParams p
        )
        {
            if (mod?.MechanicOverrides == null)
                return;
            foreach (var kv in mod.MechanicOverrides)
            {
                if (kv == null || string.IsNullOrEmpty(kv.Key))
                    continue;
                switch (kv.Key)
                {
                    // Orbit overrides applicable to projectile/aura sub-items
                    case "DestroyOnHit":
                        // Example from your JSON: Orbit sets DestroyOnHit=false -> map to projectile
                        p.projectileDestroyOnHit = ParseBool(kv.Value, p.projectileDestroyOnHit);
                        break;
                    case "OrbitRadius":
                        p.orbitRadius = ParseFloat(kv.Value, p.orbitRadius);
                        break;
                    case "OrbitSpeedDeg":
                        p.orbitSpeedDeg = ParseFloat(kv.Value, p.orbitSpeedDeg);
                        break;

                    // Drain overrides
                    case "DrainRadius":
                        p.drainRadius = ParseFloat(kv.Value, p.drainRadius);
                        break;
                    case "DrainInterval":
                        p.drainInterval = ParseFloat(kv.Value, p.drainInterval);
                        break;
                    case "DrainDamage":
                        p.drainDamage = ParseInt(kv.Value, p.drainDamage);
                        break;
                    case "LifeStealRatio":
                        p.lifeStealRatio = Mathf.Clamp01(ParseFloat(kv.Value, p.lifeStealRatio));
                        break;

                    // Generic damage/radius that apply based on primary kind
                    case "Damage":
                        if (primaryKind == MechanicKind.Projectile)
                            p.projectileDamage = ParseInt(kv.Value, p.projectileDamage);
                        else if (primaryKind == MechanicKind.Aura)
                            p.auraDamage = ParseInt(kv.Value, p.auraDamage);
                        break;
                    case "Radius":
                        if (primaryKind == MechanicKind.Aura)
                            p.auraRadius = ParseFloat(kv.Value, p.auraRadius);
                        break;
                    case "Interval":
                        if (primaryKind == MechanicKind.Aura)
                            p.auraInterval = ParseFloat(kv.Value, p.auraInterval);
                        break;
                }
            }

            // Unity-friendly explicit fields from modifier (take precedence if present)
            if (mod.DestroyOnHitOverride)
            {
                p.projectileDestroyOnHit = mod.DestroyOnHitOverride;
            }
            if (mod.OrbitRadius > 0f)
                p.orbitRadius = mod.OrbitRadius;
            if (mod.OrbitSpeedDeg > 0f)
                p.orbitSpeedDeg = mod.OrbitSpeedDeg;
            if (mod.DrainRadius > 0f)
                p.drainRadius = mod.DrainRadius;
            if (mod.DrainInterval > 0f)
                p.drainInterval = mod.DrainInterval;
            if (mod.DrainDamage > 0)
                p.drainDamage = mod.DrainDamage;
            if (mod.LifeStealRatio > 0f)
                p.lifeStealRatio = Mathf.Clamp01(mod.LifeStealRatio);
        }

        private static bool ParseBool(string s, bool fallback)
        {
            if (string.IsNullOrEmpty(s))
                return fallback;
            if (bool.TryParse(s, out var b))
                return b;
            if (int.TryParse(s, out var i))
                return i != 0;
            return fallback;
        }

        private static int ParseInt(string s, int fallback)
        {
            if (string.IsNullOrEmpty(s))
                return fallback;
            if (int.TryParse(s, out var v))
                return v;
            return fallback;
        }

        private static float ParseFloat(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s))
                return fallback;
            if (float.TryParse(s, out var v))
                return v;
            return fallback;
        }

        private static string LoadCatalog(TextAsset overrideAsset, string resourceFolder)
        {
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.text))
                return overrideAsset.text;

            var assets = Resources.LoadAll<TextAsset>(resourceFolder);
            if (assets == null || assets.Length == 0)
                return "[]";

            Array.Sort(
                assets,
                (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a?.name, b?.name)
            );

            var sb = new StringBuilder();
            sb.Append('[');
            bool wrote = false;
            foreach (var asset in assets)
            {
                if (asset == null)
                    continue;
                string text = asset.text;
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                if (wrote)
                    sb.Append(',');
                sb.Append(text.Trim());
                wrote = true;
            }
            sb.Append(']');
            return wrote ? sb.ToString() : "[]";
        }
    }
}
