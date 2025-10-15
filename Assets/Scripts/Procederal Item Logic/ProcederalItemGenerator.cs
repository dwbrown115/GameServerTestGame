using System;
using System.Collections.Generic;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

// High-level, data-driven item builder that composes existing Mechanics on sub-items (children)
// based on simple instructions from the server/client. Keeps MechanicHost intact; this is an
// alternative, more direct generator focused on performance and explicit control.
namespace Game.Procederal
{
    public enum MechanicKind
    {
        None,
        Projectile,
        Orbit,
        Aura,
        Drain,
        Beam,
        Lock,
        Track,
        Strike,
        DamageOverTime,
        Bounce,
        Explosion,
        Whip,
        Ripple,
        RippleOnHit,
        SwordSlash,
    }

    [Serializable]
    public class ItemInstruction
    {
        public string primary;
        public List<string> secondary = new List<string>();

        public MechanicKind GetPrimaryKind()
        {
            return ParseKind(primary);
        }

        public static MechanicKind ParseKind(string s)
        {
            if (string.IsNullOrEmpty(s))
                return MechanicKind.None;
            switch (s.Trim().ToLowerInvariant())
            {
                case "projectile":
                    return MechanicKind.Projectile;
                case "orbit":
                    return MechanicKind.Orbit;
                case "aura":
                    return MechanicKind.Aura;
                case "drain":
                    return MechanicKind.Drain;
                case "beam":
                    return MechanicKind.Beam;
                case "lock":
                    return MechanicKind.Lock;
                case "track":
                    return MechanicKind.Track;
                case "strike":
                    return MechanicKind.Strike;
                case "damageovertime":
                case "dot":
                    return MechanicKind.DamageOverTime;
                case "bounce":
                    return MechanicKind.Bounce;
                case "explosion":
                    return MechanicKind.Explosion;
                case "whip":
                    return MechanicKind.Whip;
                case "ripple":
                    return MechanicKind.Ripple;
                case "rippleonhit":
                    return MechanicKind.RippleOnHit;
                case "swordslash":
                case "sword":
                case "slash":
                    return MechanicKind.SwordSlash;
                default:
                    return MechanicKind.None;
            }
        }

        public bool HasSecondary(MechanicKind kind)
        {
            if (secondary == null)
                return false;
            foreach (var s in secondary)
            {
                if (ParseKind(s) == kind)
                    return true;
            }
            return false;
        }
    }

    [Serializable]
    public class ItemParams
    {
        // Common
        public int subItemCount = 1;
        public bool debugLogs = false;

        // Orbit params (modifier for sub-items like projectiles)
        public float orbitRadius = 2f;
        public float orbitSpeedDeg = 90f;
        public float startAngleDeg = 0f; // applied as base; items spaced equally beyond this

        // Projectile params
        public float projectileSize = 1f; // applied via transform scale
        public int projectileDamage = 1;
        public bool projectileDestroyOnHit = true;

        // Aura params
        public float auraRadius = 2f;
        public float auraInterval = 0.5f;
        public int auraDamage = 1;

        // Drain params
        public float drainRadius = 2f;
        public float drainInterval = 0.5f;
        public int drainDamage = 0; // optional; many implementations derive from source damage
        public float lifeStealRatio = 0.25f; // 25% of damage healed

        // Visual policy
        public bool useScaleForRadius = true; // when true, scale=radius and collider.radius=0.5f
    }

    public partial class ProcederalItemGenerator : MonoBehaviour
    {
        // Cached simple sprites
        private static Sprite _unitCircleSprite;
        private static Sprite _unitSquareSprite;

        public static Sprite GetUnitCircleSprite()
        {
            if (_unitCircleSprite != null)
                return _unitCircleSprite;
            // Create a simple white circle texture procedurally (32x32 with alpha circle)
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32[] pixels = new Color32[size * size];
            float r = size * 0.5f - 0.5f; // radius in pixels
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float dx = x - c.x;
                    float dy = y - c.y;
                    float d2 = dx * dx + dy * dy;
                    float r2 = r * r;
                    // Anti-aliased edge: soft alpha near boundary (1px band)
                    float dist = Mathf.Sqrt(d2);
                    float a = Mathf.Clamp01(r - Mathf.Abs(dist - r));
                    if (dist <= r)
                        pixels[idx] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f + 0.5f * a));
                    else
                        pixels[idx] = new Color(
                            1f,
                            1f,
                            1f,
                            Mathf.Clamp01(0.5f * Mathf.Max(0f, 1f - (dist - r)))
                        );
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _unitCircleSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
            _unitCircleSprite.name = "UnitCircleSprite";
            return _unitCircleSprite;
        }

        public static Sprite GetUnitSquareSprite()
        {
            if (_unitSquareSprite != null)
                return _unitSquareSprite;
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _unitSquareSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
            _unitSquareSprite.name = "UnitSquareSprite";
            return _unitSquareSprite;
        }

        // Moved to Core.Config.TryParseColor

        [Tooltip("Parent under which items will be created if not specified.")]
        public Transform defaultParent;

        [Tooltip("Optional owner (e.g., player) used for centering/orbits.")]
        public Transform owner;

        [Tooltip("Optional target the mechanics may center on.")]
        public Transform target;

        public bool debugLogs = false;

        [Header("Spawn Controls")]
        [Tooltip(
            "If > 0, overrides ItemParams.subItemCount and forces this many children per primary that supports multiple (e.g., Projectile, Whip)."
        )]
        public int generatorChildrenCount = 0;

        [Header("Modifier Auto-Apply")]
        [Tooltip(
            "When true, any modifiers listed in the instruction will be auto-applied wherever compatible (sub-items and spawner children)."
        )]
        public bool autoApplyCompatibleModifiers = true;

        [Tooltip(
            "Modifier names to skip globally when auto-applying (case-insensitive). Example: Bounce, Orbit."
        )]
        public List<string> autoApplyModifierDenyList = new List<string>();

        [Header("Mechanic Lists (JSON)")]
        [Tooltip(
            "Optional override catalog for primary mechanics. Leave null to load all JSON files under Resources/ProcederalMechanics/Primary."
        )]
        public TextAsset primaryMechanicListJson;

        [Tooltip(
            "Optional override catalog for modifiers. Leave null to load all JSON files under Resources/ProcederalMechanics/Modifier."
        )]
        public TextAsset modifierMechanicListJson;

        // Entry point: JSON -> build
        public GameObject CreateFromJson(string json, ItemParams p = null, Transform parent = null)
        {
            var instruction = JsonUtility.FromJson<ItemInstruction>(json);
            return Create(instruction, p, parent);
        }

        // Entry point: Instruction -> build
        public GameObject Create(
            ItemInstruction instruction,
            ItemParams p = null,
            Transform parent = null
        )
        {
            if (instruction == null)
            {
                Log("No instruction provided.");
                return null;
            }
            p ??= new ItemParams();

            // Ensure central registry is initialized (enables future OTA swaps)
            MechanicsRegistry.Instance.EnsureInitialized(
                primaryMechanicListJson,
                modifierMechanicListJson
            );

            string secondaryLabel =
                (instruction.secondary != null && instruction.secondary.Count > 0)
                    ? "+" + string.Join("+", instruction.secondary)
                    : string.Empty;
            var root = new GameObject($"Item_{instruction.primary}{secondaryLabel}");
            root.transform.SetParent(
                parent != null ? parent : (defaultParent != null ? defaultParent : transform),
                false
            );
            root.transform.localPosition = Vector3.zero;

            var subItems = new List<GameObject>();

            var kind = instruction.GetPrimaryKind();
            var builder = Game.Procederal.Core.Builders.PrimaryBuilders.Get(kind);
            if (builder == null)
            {
                Log($"Unsupported primary mechanic '{instruction.primary}'.");
            }
            else
            {
                builder.Build(this, root, instruction, p, subItems);
            }
            if (autoApplyCompatibleModifiers && subItems.Count > 0)
            {
                foreach (var mk in GetModifiersToApply(instruction))
                    AddModifierToAll(subItems, mk, p);
            }

            // Ensure a runner exists to drive IMechanic.Tick for generated mechanics
            var runner = root.GetComponent<Game.Procederal.Api.MechanicRunner>();
            if (runner == null)
                runner = root.AddComponent<Game.Procederal.Api.MechanicRunner>();
            runner.debugLogs = debugLogs;
            runner.RegisterTree(root.transform);

            return root;
        }

        public Component AddMechanicByName(
            GameObject go,
            string mechanicName,
            (string key, object val)[] settings
        )
        {
            // Load catalog from the same JSONs you already maintain in your project via Resources
            // or expose them in the inspector for this generator. To keep it decoupled here,
            // we try Resources first.
            if (
                !MechanicsRegistry.Instance.TryGetPath(mechanicName, out var path)
                || string.IsNullOrWhiteSpace(path)
            )
            {
                Log($"Mechanic path not found for '{mechanicName}'.");
                return null;
            }
            var type = MechanicReflection.ResolveTypeFromMechanicPath(path);
            if (type == null)
            {
                Log($"Failed to resolve mechanic type for '{mechanicName}' from '{path}'.");
                return null;
            }
            // Merge defaults from JSON Properties with optional Overrides, then apply provided settings last
            var merged = MechanicsRegistry.Instance.GetMergedSettings(mechanicName);
            if (settings != null)
            {
                foreach (var (key, val) in settings)
                {
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    merged[key] = val;
                }
            }
            var finalList = new List<(string key, object val)>();
            foreach (var kv in merged)
                finalList.Add((kv.Key, kv.Value));
            return MechanicReflection.AddMechanicWithSettings(go, type, finalList.ToArray());
        }

        private bool SetExistingMechanicSetting(
            GameObject go,
            string mechanicName,
            string key,
            object val
        )
        {
            if (go == null)
                return false;
            if (
                !MechanicsRegistry.Instance.TryGetPath(mechanicName, out var path)
                || string.IsNullOrWhiteSpace(path)
            )
                return false;
            var type = MechanicReflection.ResolveTypeFromMechanicPath(path);
            if (type == null)
                return false;
            var comp = go.GetComponent(type) as Component;
            if (comp == null)
                return false;
            return MechanicReflection.ApplyMember(comp, key, val);
        }

        private bool HasMechanic(GameObject go, string mechanicNameOrPath)
        {
            if (go == null || string.IsNullOrWhiteSpace(mechanicNameOrPath))
                return false;

            // Prefer registry lookup when using mechanic name tokens
            if (
                MechanicsRegistry.Instance.TryGetPath(mechanicNameOrPath, out var path)
                && !string.IsNullOrWhiteSpace(path)
            )
            {
                var resolved = MechanicReflection.ResolveTypeFromMechanicPath(path);
                if (resolved != null && go.GetComponent(resolved) != null)
                    return true;
            }

            // Fallback: treat identifier as a type or asset path
            var directType = MechanicReflection.ResolveTypeFromMechanicPath(mechanicNameOrPath);
            if (directType != null && go.GetComponent(directType) != null)
                return true;

            return false;
        }

        public void InitializeMechanics(GameObject go, Transform ownerT, Transform targetT)
        {
            if (go == null)
                return;
            var ctx = new MechanicContext
            {
                Owner = ownerT != null ? ownerT : transform,
                Payload = go.transform,
                Target = targetT,
                OwnerRb2D = ownerT != null ? ownerT.GetComponent<Rigidbody2D>() : null,
                PayloadRb2D = go.GetComponent<Rigidbody2D>(),
            };
            foreach (var m in go.GetComponents<MonoBehaviour>())
            {
                if (m is IMechanic mech)
                {
                    mech.Initialize(ctx);
                }
            }
        }

        private void PlaceAtPolar(Transform t, float angleDeg, float radius)
        {
            var a = angleDeg * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            t.localPosition = pos;
        }

        // TrySet no longer needed here; reflection handled centrally via MechanicReflection

        private void Log(string msg)
        {
            if (debugLogs)
                Debug.Log($"[ProcederalItemGenerator] {msg}", this);
        }

        // Generic: forward all compatible modifiers to a spawner object that exposes AddModifierSpec(string, params (string,object)[])
        // Also handles small special-cases: Orbit (skip: handled via routing), Drain (ensure owner has Drain), Track (optionally set aim flag if present)
        internal void ForwardModifiersToSpawner(
            object spawner,
            ItemInstruction instruction,
            ItemParams p
        )
        {
            if (spawner == null || instruction == null)
                return;
            var mods = GetModifiersToApply(instruction);
            if (mods == null || mods.Count == 0)
                return;

            // Reflection helpers
            bool TryAddSpec(string mechName, params (string key, object val)[] settings)
            {
                var type = spawner.GetType();
                var mi = type.GetMethod("AddModifierSpec");
                if (mi == null)
                {
                    // Some spawners may have params signature resolution; try any AddModifierSpec
                    foreach (var m in type.GetMethods())
                    {
                        if (m.Name == "AddModifierSpec" && m.GetParameters().Length >= 1)
                        {
                            mi = m;
                            break;
                        }
                    }
                }
                if (mi == null)
                    return false;
                // Build ValueTuple<string, object>[] via reflection to match params signature
                var tupleType = typeof(System.ValueTuple<string, object>);
                System.Array tupleArray;
                if (settings != null && settings.Length > 0)
                {
                    tupleArray = System.Array.CreateInstance(tupleType, settings.Length);
                    for (int i = 0; i < settings.Length; i++)
                    {
                        var vt = System.Activator.CreateInstance(
                            tupleType,
                            settings[i].key,
                            settings[i].val
                        );
                        tupleArray.SetValue(vt, i);
                    }
                }
                else
                {
                    tupleArray = System.Array.CreateInstance(tupleType, 0);
                }
                mi.Invoke(spawner, new object[] { mechName, tupleArray });
                return true;
            }

            // Try to get owner transform off spawner for Drain owner ensure
            Transform SpawnerOwner()
            {
                var t = spawner.GetType();
                try
                {
                    var fi = t.GetField("owner");
                    if (fi != null)
                    {
                        var val = fi.GetValue(spawner) as Transform;
                        if (val != null)
                            return val;
                    }
                    var pi = t.GetProperty("owner");
                    if (pi != null && pi.CanRead)
                    {
                        var val = pi.GetValue(spawner) as Transform;
                        if (val != null)
                            return val;
                    }
                }
                catch { }
                return owner != null ? owner : transform;
            }

            foreach (var kind in mods)
            {
                switch (kind)
                {
                    case MechanicKind.Orbit:
                        // handled by builder routing; do not attach as a modifier to spawner children
                        continue;
                    case MechanicKind.Drain:
                        EnsureOwnerDrain(SpawnerOwner(), p);
                        TryAddSpec("Drain", ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio)));
                        break;
                    case MechanicKind.Track:
                        // Attach Track and, if the spawner supports, enable aim at nearest enemy
                        TryAddSpec("Track");
                        var aimField = spawner.GetType().GetField("aimAtNearestEnemy");
                        if (aimField != null && aimField.FieldType == typeof(bool))
                        {
                            aimField.SetValue(spawner, true);
                        }
                        else
                        {
                            var aimProp = spawner.GetType().GetProperty("aimAtNearestEnemy");
                            if (
                                aimProp != null
                                && aimProp.CanWrite
                                && aimProp.PropertyType == typeof(bool)
                            )
                                aimProp.SetValue(spawner, true);
                        }
                        break;
                    case MechanicKind.DamageOverTime:
                        TryAddSpec("DamageOverTime");
                        break;
                    case MechanicKind.Bounce:
                        TryAddSpec("Bounce");
                        break;
                    case MechanicKind.Explosion:
                        TryAddSpec("Explosion");
                        break;
                    case MechanicKind.RippleOnHit:
                        TryAddSpec("RippleOnHit");
                        break;
                    case MechanicKind.Lock:
                        TryAddSpec("Lock");
                        break;
                    default:
                        // For future modifiers, default to name matching enum
                        TryAddSpec(kind.ToString());
                        break;
                }
            }
        }

        // Small JSON helpers
        private static void ReadIf(Dictionary<string, object> dict, string key, ref int val)
        {
            if (dict != null && dict.TryGetValue(key, out var v))
            {
                if (v is int vi)
                    val = vi;
                else if (v is float vf)
                    val = Mathf.RoundToInt(vf);
                else if (v is string vs && int.TryParse(vs, out var pi))
                    val = pi;
            }
        }

        private static void ReadIf(Dictionary<string, object> dict, string key, ref float val)
        {
            if (dict != null && dict.TryGetValue(key, out var v))
            {
                if (v is float vf)
                    val = vf;
                else if (v is int vi)
                    val = vi;
                else if (v is string vs && float.TryParse(vs, out var pf))
                    val = pf;
            }
        }

        private static void ReadIf(Dictionary<string, object> dict, string key, ref bool val)
        {
            if (dict != null && dict.TryGetValue(key, out var v))
            {
                if (v is bool vb)
                    val = vb;
                else if (v is string vs && bool.TryParse(vs, out var pb))
                    val = pb;
            }
        }

        // --- Auto-apply helpers ---
        private bool IsDenied(string modifierName)
        {
            if (autoApplyModifierDenyList == null || autoApplyModifierDenyList.Count == 0)
                return false;
            foreach (var s in autoApplyModifierDenyList)
            {
                if (
                    !string.IsNullOrWhiteSpace(s)
                    && !string.IsNullOrWhiteSpace(modifierName)
                    && string.Equals(
                        s.Trim(),
                        modifierName.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return true;
            }
            return false;
        }

        internal HashSet<MechanicKind> GetModifiersToApply(ItemInstruction instruction)
        {
            var set = new HashSet<MechanicKind>();
            if (
                !autoApplyCompatibleModifiers
                || instruction == null
                || instruction.secondary == null
            )
                return set;
            foreach (var s in instruction.secondary)
            {
                var kind = ItemInstruction.ParseKind(s);
                if (kind == MechanicKind.None)
                    continue;
                string modName = s?.Trim();
                if (string.IsNullOrWhiteSpace(modName) || IsDenied(modName))
                    continue;
                if (!ShouldApplyModifierForPrimary(instruction.primary, modName))
                    continue;
                set.Add(kind);
            }
            return set;
        }

        private bool ShouldApplyModifierForPrimary(string primaryName, string modifierName)
        {
            if (string.IsNullOrWhiteSpace(primaryName) || string.IsNullOrWhiteSpace(modifierName))
                return false;
            // Check JSON-based incompatibilities from Primary Mechanic List
            var incompatible = LoadStringArrayForMechanic(primaryName, "IncompatibleWith");
            foreach (var s in incompatible)
            {
                if (string.Equals(s, modifierName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private List<string> LoadStringArrayForMechanic(string mechanicName, string arrayName)
        {
            return MechanicsRegistry.Instance.GetIncompatibleWith(mechanicName);
        }

        internal void EnsureOwnerDrain(Transform drainOwner, ItemParams p)
        {
            if (drainOwner == null)
                return;
            var existingDrain =
                drainOwner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
            if (existingDrain == null)
            {
                AddMechanicByName(
                    drainOwner.gameObject,
                    "Drain",
                    new (string key, object val)[]
                    {
                        ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio)),
                        ("debugLogs", p.debugLogs || debugLogs),
                    }
                );
                InitializeMechanics(drainOwner.gameObject, drainOwner, target);
            }
        }

        // --- JSON settings helpers ---
        internal Dictionary<string, object> LoadAndMergeJsonSettings(string mechanicName)
        {
            var dict = MechanicsRegistry.Instance.GetMergedSettings(mechanicName);
            NormalizeSettings(mechanicName, dict);
            return dict;
        }

        // Exposed for trusted internal API components (e.g., WhipArcSet)
        public static Dictionary<string, object> LoadAndMergeForExternal(string mechanicName)
        {
            // Create a temporary generator to leverage JSON resolution logic; we rely on Resources fallback
            var temp = new GameObject("_JsonHelper").AddComponent<ProcederalItemGenerator>();
            try
            {
                var src = temp.LoadAndMergeJsonSettings(mechanicName);
                return new Dictionary<string, object>(src, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                if (temp != null)
                    GameObject.DestroyImmediate(temp.gameObject);
            }
        }

        internal Dictionary<string, object> LoadKvpArrayForMechanic(
            string mechanicName,
            string arrayName
        )
        {
            return MechanicsRegistry.Instance.GetKvpArray(mechanicName, arrayName);
        }

        private void NormalizeSettings(string mechanicName, Dictionary<string, object> dict)
        {
            if (dict == null)
                return;
            // Add camelCase aliases for PascalCase keys
            var toAdd = new List<KeyValuePair<string, object>>();
            foreach (var kv in dict)
            {
                string k = kv.Key;
                if (!string.IsNullOrEmpty(k) && char.IsUpper(k[0]))
                {
                    string camel = char.ToLowerInvariant(k[0]) + k.Substring(1);
                    if (!dict.ContainsKey(camel))
                        toAdd.Add(new KeyValuePair<string, object>(camel, kv.Value));
                }
            }
            foreach (var kv in toAdd)
                dict[kv.Key] = kv.Value;

            // Mechanic-specific normalization
            if (string.Equals(mechanicName, "Projectile", StringComparison.OrdinalIgnoreCase))
            {
                if (dict.TryGetValue("direction", out var dirVal) && dirVal is string ds)
                {
                    Vector2 v = Vector2.right;
                    switch (ds.Trim().ToLowerInvariant())
                    {
                        case "left":
                            v = Vector2.left;
                            break;
                        case "up":
                            v = Vector2.up;
                            break;
                        case "down":
                            v = Vector2.down;
                            break;
                        case "right":
                        default:
                            v = Vector2.right;
                            break;
                    }
                    dict["direction"] = v;
                }
            }
            else if (string.Equals(mechanicName, "Orbit", StringComparison.OrdinalIgnoreCase))
            {
                if (dict.TryGetValue("direction", out var dval) && dval is string ds)
                {
                    float current = 90f;
                    if (dict.TryGetValue("angularSpeedDeg", out var ang))
                    {
                        if (ang is float f)
                            current = f;
                        else if (ang is int i)
                            current = i;
                        else if (ang is string s && float.TryParse(s, out var pf))
                            current = pf;
                    }
                    string tok = ds.Trim().ToLowerInvariant();
                    if (tok == "clockwise")
                        current = -Mathf.Abs(current);
                    else
                        current = Mathf.Abs(current);
                    dict["angularSpeedDeg"] = current;
                    dict.Remove("direction");
                }
            }
        }
    }
}
