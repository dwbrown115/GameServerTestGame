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
        DamageZone,
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
                case "damagezone":
                case "damage-zone":
                case "zone":
                    return MechanicKind.DamageZone;
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

        // Damage Zone params
        public float damageZoneRadius = 2f;
        public float damageZoneInterval = 0.5f;
        public int damageZoneDamage = 2;
        public float damageZoneLifetime = 4f;

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

        [Header("Catalog (optional)")]
        [Tooltip(
            "Provide an object implementing IMechanicCatalog or IMechanicCatalogProvider to control catalog bootstrap."
        )]
        [SerializeField]
        private UnityEngine.Object mechanicCatalogSource;

        [Header("Pooling (optional)")]
        [Tooltip(
            "Provide an object implementing IItemObjectFactory or IItemObjectFactoryProvider to control how payload GameObjects are created and recycled."
        )]
        [SerializeField]
        private UnityEngine.Object objectFactorySource;

        private IMechanicCatalog _catalog;
        private MechanicSettingsCache _settingsCache;
        private bool _catalogFallbackWarned;
        private IItemObjectFactory _objectFactory;
        private bool _objectFactoryFallbackWarned;

        private readonly List<ModifierFitResult> _modifierFitResults =
            new List<ModifierFitResult>();
        private readonly HashSet<string> _softFitNotices = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );

        public void SetMechanicCatalog(IMechanicCatalog catalog)
        {
            AssignCatalog(catalog);
        }

        public void SetObjectFactory(IItemObjectFactory factory)
        {
            AssignObjectFactory(factory);
        }

        public IReadOnlyList<ModifierFitResult> LastModifierFits => _modifierFitResults;

        public enum ModifierFitSeverity
        {
            Normal = 0,
            Caution = 1,
            Blocked = 2,
        }

        public struct ModifierFitResult
        {
            public string primary;
            public string modifier;
            public MechanicKind kind;
            public float score;
            public ModifierFitSeverity severity;
            public string reason;
        }

        private IMechanicCatalog GetCatalog()
        {
            if (_catalog != null)
                return _catalog;

            if (mechanicCatalogSource is IMechanicCatalogInitializer initializer)
            {
                initializer.EnsureReady();
            }

            IMechanicCatalog candidate = null;
            if (mechanicCatalogSource is IMechanicCatalog directCatalog)
            {
                candidate = directCatalog;
            }
            else if (mechanicCatalogSource is IMechanicCatalogProvider provider)
            {
                candidate = provider.Catalog;
            }

            if (candidate != null)
            {
                AssignCatalog(candidate);
                return _catalog;
            }

            if (!_catalogFallbackWarned)
            {
                Debug.LogWarning(
                    "[ProcederalItemGenerator] No mechanic catalog provided; falling back to MechanicsRegistry.Instance. Ensure an external bootstrapper initializes the registry before use.",
                    this
                );
                _catalogFallbackWarned = true;
            }
            var registry = MechanicsRegistry.Instance;
            registry.EnsureInitialized(primaryMechanicListJson, modifierMechanicListJson);

            AssignCatalog(registry);
            return _catalog;
        }

        private MechanicSettingsCache GetSettingsCache()
        {
            var catalog = GetCatalog();
            if (_settingsCache == null || !ReferenceEquals(_settingsCache.Catalog, catalog))
                _settingsCache = new MechanicSettingsCache(catalog);
            return _settingsCache;
        }

        private IItemObjectFactory GetObjectFactory()
        {
            if (_objectFactory != null)
                return _objectFactory;

            if (objectFactorySource is IItemObjectFactoryInitializer initializer)
            {
                initializer.EnsureReady();
            }

            IItemObjectFactory candidate = null;
            if (objectFactorySource is IItemObjectFactory directFactory)
            {
                candidate = directFactory;
            }
            else if (objectFactorySource is IItemObjectFactoryProvider provider)
            {
                candidate = provider.Factory;
            }

            if (candidate != null)
            {
                AssignObjectFactory(candidate);
                return _objectFactory;
            }

            if (!_objectFactoryFallbackWarned)
            {
                Debug.Log(
                    "[ProcederalItemGenerator] No object factory supplied; falling back to ItemObjectFactoryLocator. Register a custom pool via SetObjectFactory or objectFactorySource if reuse is desired.",
                    this
                );
                _objectFactoryFallbackWarned = true;
            }

            AssignObjectFactory(ItemObjectFactoryLocator.Factory);
            return _objectFactory;
        }

        public void InvalidateMechanicSettings(string mechanicName = null)
        {
            if (_settingsCache == null)
                return;
            if (string.IsNullOrWhiteSpace(mechanicName))
                _settingsCache.Clear();
            else
                _settingsCache.Invalidate(mechanicName);
        }

        private void AssignCatalog(IMechanicCatalog catalog)
        {
            if (catalog == null)
            {
                _catalog = null;
                _settingsCache = null;
                _modifierFitResults.Clear();
                _softFitNotices.Clear();
                return;
            }

            if (!ReferenceEquals(_catalog, catalog))
            {
                _catalog = catalog;
                _settingsCache = new MechanicSettingsCache(catalog);
                _modifierFitResults.Clear();
                _softFitNotices.Clear();
            }
            else if (_settingsCache == null)
            {
                _settingsCache = new MechanicSettingsCache(catalog);
            }
        }

        private void AssignObjectFactory(IItemObjectFactory factory)
        {
            _objectFactory = factory ?? ItemObjectFactoryLocator.Factory;
        }

        private GameObject AcquireObject(
            string key,
            Transform parent,
            bool worldPositionStays = false
        )
        {
            var factory = GetObjectFactory();
            var go = factory?.Acquire(key, parent, worldPositionStays);
            if (go == null)
            {
                go = new GameObject(string.IsNullOrWhiteSpace(key) ? "GeneratorItem" : key);
                if (parent != null)
                    go.transform.SetParent(parent, worldPositionStays);
            }
            else if (parent != null && go.transform.parent != parent)
            {
                go.transform.SetParent(parent, worldPositionStays);
            }
            else if (parent == null)
            {
                go.transform.SetParent(null, false);
            }

            go.name = string.IsNullOrWhiteSpace(key) ? go.name : key;
            ResetReusableObject(go);

            var t = go.transform;
            if (!worldPositionStays)
                t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            go.SetActive(true);

            return go;
        }

        private void ResetReusableObject(GameObject go)
        {
            if (go == null)
                return;

            var components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null || comp is Transform)
                    continue;
                if (ReferenceEquals(comp, this))
                    continue;
                if (comp is IPooledPayloadResettable resettable)
                {
                    resettable.ResetForPool();
                    continue;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(comp);
                    continue;
                }
#endif
                Destroy(comp);
            }

            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var child = go.transform.GetChild(i);
                if (child == null)
                    continue;
                var childGo = child.gameObject;
                child.SetParent(null, false);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(childGo);
                    continue;
                }
#endif
                Destroy(childGo);
            }
        }

        public void ReleaseObject(GameObject go)
        {
            if (go == null)
                return;
            go.transform.SetParent(null, false);
            GetObjectFactory().Release(go);
        }

        public void ReleaseTree(GameObject root)
        {
            if (root == null)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = transforms.Length - 1; i >= 0; --i)
            {
                var t = transforms[i];
                if (t == null)
                    continue;
                ReleaseObject(t.gameObject);
            }
        }

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

            string secondaryLabel =
                (instruction.secondary != null && instruction.secondary.Count > 0)
                    ? "+" + string.Join("+", instruction.secondary)
                    : string.Empty;
            var parentTransform =
                parent != null ? parent : (defaultParent != null ? defaultParent : transform);
            var rootName = $"Item_{instruction.primary}{secondaryLabel}";
            var root = AcquireObject(rootName, parentTransform);
            if (root == null)
            {
                Log("Failed to acquire root GameObject for generated item.");
                return null;
            }

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
            var catalog = GetCatalog();
            var cache = GetSettingsCache();
            if (
                !MechanicApplier.TryPrepare(
                    catalog,
                    cache,
                    mechanicName,
                    settings,
                    out var type,
                    out var finalSettings,
                    out var failure
                )
            )
            {
                if (!string.IsNullOrEmpty(failure))
                    Log(failure);
                return null;
            }

            return MechanicReflection.AddMechanicWithSettings(go, type, finalSettings);
        }

        public bool SetExistingMechanicSetting(
            GameObject go,
            string mechanicName,
            string key,
            object val
        )
        {
            if (go == null)
                return false;
            var catalog = GetCatalog();
            if (!MechanicApplier.TryResolveType(catalog, mechanicName, out var type, out _, out _))
                return false;
            var comp = go.GetComponent(type) as Component;
            if (comp == null)
                return false;
            return MechanicReflection.ApplyMember(comp, key, val);
        }

        public bool HasMechanic(GameObject go, string mechanicNameOrPath)
        {
            if (go == null || string.IsNullOrWhiteSpace(mechanicNameOrPath))
                return false;

            // Prefer registry lookup when using mechanic name tokens
            if (
                MechanicApplier.TryResolveType(
                    GetCatalog(),
                    mechanicNameOrPath,
                    out var resolvedType,
                    out _,
                    out _
                )
            )
            {
                if (resolvedType != null && go.GetComponent(resolvedType) != null)
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

        public void Log(string msg)
        {
            if (debugLogs)
                Debug.Log($"[ProcederalItemGenerator] {msg}", this);
        }

        // Generic: forward all compatible modifiers to a spawner object that exposes AddModifierSpec(string, params (string,object)[])
        // Also handles small special-cases: Orbit (skip: handled via routing), Drain (ensure owner has Drain), Track (optionally set aim flag if present)
        internal void ForwardModifiersToSpawner(
            IModifierReceiver spawner,
            ItemInstruction instruction,
            ItemParams p
        )
        {
            if (spawner == null || instruction == null)
                return;
            var mods = GetModifiersToApply(instruction);
            if (mods == null || mods.Count == 0)
                return;

            var ownerProvider = spawner as IModifierOwnerProvider;
            var aimToggle = spawner as IAimAtNearestEnemyToggle;
            Transform modifierOwner = ownerProvider?.ModifierOwner;
            if (modifierOwner == null)
            {
                if (spawner is Component component)
                    modifierOwner = component.transform;
                else
                    modifierOwner = owner != null ? owner : transform;
            }

            foreach (var kind in mods)
            {
                switch (kind)
                {
                    case MechanicKind.Orbit:
                        // handled by builder routing; do not attach as a modifier to spawner children
                        continue;
                    case MechanicKind.Drain:
                        EnsureOwnerDrain(modifierOwner, p);
                        spawner.AddModifierSpec(
                            "Drain",
                            ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio))
                        );
                        break;
                    case MechanicKind.Track:
                        // Attach Track and, if the spawner supports, enable aim at nearest enemy
                        spawner.AddModifierSpec("Track");
                        if (aimToggle != null)
                            aimToggle.AimAtNearestEnemy = true;
                        break;
                    case MechanicKind.DamageOverTime:
                        spawner.AddModifierSpec("DamageOverTime");
                        break;
                    case MechanicKind.Bounce:
                        spawner.AddModifierSpec("Bounce");
                        break;
                    case MechanicKind.Explosion:
                        spawner.AddModifierSpec("Explosion");
                        break;
                    case MechanicKind.RippleOnHit:
                        spawner.AddModifierSpec("RippleOnHit");
                        break;
                    case MechanicKind.Lock:
                        spawner.AddModifierSpec("Lock");
                        break;
                    default:
                        // For future modifiers, default to name matching enum
                        spawner.AddModifierSpec(kind.ToString());
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
            _modifierFitResults.Clear();

            if (
                !autoApplyCompatibleModifiers
                || instruction == null
                || instruction.secondary == null
            )
                return set;

            var catalog = GetCatalog();
            var primaryName = instruction.primary?.Trim();

            foreach (var s in instruction.secondary)
            {
                var kind = ItemInstruction.ParseKind(s);
                if (kind == MechanicKind.None)
                    continue;

                string modName = s?.Trim();
                if (string.IsNullOrWhiteSpace(modName) || IsDenied(modName))
                    continue;

                var fit = EvaluateModifierFit(catalog, primaryName, modName, kind);
                if (fit.severity == ModifierFitSeverity.Blocked)
                {
                    if (!string.IsNullOrEmpty(fit.reason))
                    {
                        Debug.LogWarning(
                            $"[ProcederalItemGenerator] Blocked modifier '{modName}' for '{primaryName}' ({fit.reason}).",
                            this
                        );
                    }
                    continue;
                }

                if (!set.Add(kind))
                    continue;

                _modifierFitResults.Add(fit);

                if (fit.severity == ModifierFitSeverity.Caution)
                {
                    var key = $"{fit.primary}|{fit.modifier}";
                    if (_softFitNotices.Add(key))
                    {
                        var reasonSuffix = string.IsNullOrEmpty(fit.reason)
                            ? string.Empty
                            : $" ({fit.reason})";
                        Debug.LogWarning(
                            $"[ProcederalItemGenerator] Soft compatibility warning: {fit.primary} + {fit.modifier} score {fit.score:0.00}{reasonSuffix}.",
                            this
                        );
                    }
                }
            }

            return set;
        }

        private ModifierFitResult EvaluateModifierFit(
            IMechanicCatalog catalog,
            string primaryName,
            string modifierName,
            MechanicKind kind
        )
        {
            var result = new ModifierFitResult
            {
                primary = primaryName ?? string.Empty,
                modifier = modifierName ?? string.Empty,
                kind = kind,
                score = 1f,
                severity = ModifierFitSeverity.Normal,
                reason = string.Empty,
            };

            if (string.IsNullOrWhiteSpace(primaryName) || string.IsNullOrWhiteSpace(modifierName))
            {
                result.score = 0f;
                result.severity = ModifierFitSeverity.Blocked;
                result.reason = "Missing mechanic identifier.";
                return result;
            }

            List<string> reasons = null;

            var incompatible = catalog.GetIncompatibleWith(primaryName);
            if (incompatible != null)
            {
                foreach (var entry in incompatible)
                {
                    if (string.Equals(entry, modifierName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.score = 0.2f;
                        result.severity = ModifierFitSeverity.Caution;
                        reasons ??= new List<string>();
                        reasons.Add("Listed as incompatible in catalog");
                        break;
                    }
                }
            }

            if (!catalog.TryGetPath(modifierName, out _))
            {
                result.score = Math.Min(result.score, 0.3f);
                if (result.severity < ModifierFitSeverity.Caution)
                    result.severity = ModifierFitSeverity.Caution;
                reasons ??= new List<string>();
                reasons.Add("Modifier not found in catalog");
            }

            if (string.Equals(primaryName, modifierName, StringComparison.OrdinalIgnoreCase))
            {
                result.score = Math.Min(result.score, 0.4f);
                if (result.severity < ModifierFitSeverity.Caution)
                    result.severity = ModifierFitSeverity.Caution;
                reasons ??= new List<string>();
                reasons.Add("Modifier matches primary mechanic");
            }

            result.reason =
                reasons != null && reasons.Count > 0 ? string.Join("; ", reasons) : string.Empty;
            return result;
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
            var cache = GetSettingsCache();
            return cache.Get(mechanicName, dict => NormalizeSettings(mechanicName, dict));
        }

        // Exposed for trusted internal API components (e.g., WhipArcSet)
        public static Dictionary<string, object> LoadAndMergeForExternal(string mechanicName)
        {
            // Create a temporary generator to leverage JSON resolution logic; we rely on Resources fallback
            var temp = new GameObject("_JsonHelper").AddComponent<ProcederalItemGenerator>();
            try
            {
                temp.SetMechanicCatalog(MechanicsRegistry.Instance);
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
            return GetCatalog().GetKvpArray(mechanicName, arrayName);
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
