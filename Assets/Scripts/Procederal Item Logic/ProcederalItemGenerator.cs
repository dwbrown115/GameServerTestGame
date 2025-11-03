using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

    public enum ChildBehaviorSelection
    {
        Unspecified = 0,
        Shoot,
        Drop,
        Throw,
        BoundToOrigin,
    }

    [Serializable]
    public struct ChildBehaviorOverrides
    {
        [Tooltip("Movement override to apply to generated children.")]
        public ChildBehaviorSelection movement;

        [Tooltip("Orbit path identifier for orbit-style movement (e.g., 'Circular').")]
        public string orbitPath;

        public static ChildBehaviorOverrides Default =>
            new ChildBehaviorOverrides
            {
                movement = ChildBehaviorSelection.Shoot,
                orbitPath = "Circular",
            };

        public ChildBehaviorSelection ResolveMovementOrDefault()
        {
            return movement == ChildBehaviorSelection.Unspecified
                ? ChildBehaviorSelection.Shoot
                : movement;
        }

        public string ResolveOrbitPathOrDefault()
        {
            return string.IsNullOrWhiteSpace(orbitPath) ? "Circular" : orbitPath.Trim();
        }
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
        public ChildBehaviorOverrides childBehavior = ChildBehaviorOverrides.Default;

        // Orbit params (modifier for sub-items like projectiles)
        public float orbitRadius = 2f;
        public float orbitSpeedDeg = 90f;
        public float startAngleDeg = 0f; // applied as base; items spaced equally beyond this
        public float orbitPathRotationBaseDeg = 0f;
        public float orbitPathRotationStepDeg = 0f;

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

        [Header("Debug Output")]
        [Tooltip(
            "Delete generated mechanic dump files when the game ends or the application quits."
        )]
        public bool deleteMechanicDumpOnShutdown = true;

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

        private static readonly List<string> _mechanicDumpFiles = new List<string>();
        private static readonly object _mechanicDumpLock = new object();
        private static bool _mechanicDumpCleanupRegistered = false;
        private MechanicDumpContext _activeDumpContext;

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

            var parentTransform =
                parent != null ? parent : (defaultParent != null ? defaultParent : transform);
            string baseName = BuildMechanicDumpBaseName(instruction);
            var root = AcquireObject("GeneratedItem", parentTransform);
            root.name = baseName;
            if (root == null)
            {
                Log("Failed to acquire root GameObject for generated item.");
                return null;
            }

            var subItems = new List<GameObject>();
            var previousContext = _activeDumpContext;
            var context = new MechanicDumpContext();
            _activeDumpContext = context;
            try
            {
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

                TryWriteMechanicDump(root, instruction, baseName);
            }
            finally
            {
                _activeDumpContext = previousContext;
            }

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

            var component = MechanicReflection.AddMechanicWithSettings(go, type, finalSettings);
            RegisterMechanicDumpEntry(go, mechanicName, type, finalSettings);
            return component;
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
                if (
                    resolvedType != null
                    && (
                        typeof(Component).IsAssignableFrom(resolvedType) || resolvedType.IsInterface
                    )
                    && go.GetComponent(resolvedType) != null
                )
                    return true;
            }

            // Fallback: treat identifier as a type or asset path
            var directType = MechanicReflection.ResolveTypeFromMechanicPath(mechanicNameOrPath);
            if (
                directType != null
                && (typeof(Component).IsAssignableFrom(directType) || directType.IsInterface)
                && go.GetComponent(directType) != null
            )
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

        private void RegisterMechanicDumpEntry(
            GameObject go,
            string mechanicName,
            Type mechanicType,
            (string key, object value)[] settings
        )
        {
            if (_activeDumpContext == null)
                return;

            var entry = BuildMechanicDumpEntry(
                mechanicName,
                go,
                mechanicType,
                settings,
                fromInstruction: false
            );
            if (entry == null)
                return;

            _activeDumpContext.AddOrMerge(entry);
        }

        private MechanicDumpEntry BuildMechanicDumpEntry(
            string mechanicName,
            GameObject go,
            Type mechanicType,
            (string key, object value)[] settings,
            bool fromInstruction
        )
        {
            string resolvedName = !string.IsNullOrWhiteSpace(mechanicName)
                ? mechanicName.Trim()
                : (mechanicType != null ? mechanicType.Name : string.Empty);
            if (string.IsNullOrWhiteSpace(resolvedName) && mechanicType == null)
                return null;

            var entry = new MechanicDumpEntry
            {
                mechanicName = resolvedName,
                mechanicType = mechanicType != null ? mechanicType.FullName : string.Empty,
                componentShortName = mechanicType != null ? mechanicType.Name : string.Empty,
                componentAssemblyQualifiedName =
                    mechanicType != null ? mechanicType.AssemblyQualifiedName : string.Empty,
                gameObject = GetHierarchyPath(go),
                appliedSettings = ConvertSettingsForDump(settings),
                fromInstruction = fromInstruction,
            };

            PopulateCatalogDetails(entry);
            return entry;
        }

        private void PopulateCatalogDetails(MechanicDumpEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.mechanicName))
                return;

            var registry = MechanicsRegistry.Instance;
            registry.EnsureInitialized(primaryMechanicListJson, modifierMechanicListJson);

            if (registry.TryGetManifestEntry(entry.mechanicName, out var manifestEntry))
            {
                if (!string.IsNullOrWhiteSpace(manifestEntry.category))
                    entry.mechanicCategory = manifestEntry.category;
                if (!string.IsNullOrWhiteSpace(manifestEntry.attribute))
                    entry.mechanicAttribute = manifestEntry.attribute;
            }

            if (
                registry.TryGetPath(entry.mechanicName, out var path)
                && !string.IsNullOrWhiteSpace(path)
            )
            {
                entry.mechanicPath = path;
            }

            entry.generator = ConvertCatalogMapToList(
                registry.GetKvpArray(entry.mechanicName, "Generator")
            );
            entry.properties = ConvertCatalogMapToList(
                registry.GetKvpArray(entry.mechanicName, "Properties")
            );
            entry.overrides = ConvertCatalogMapToList(
                registry.GetKvpArray(entry.mechanicName, "Overrides")
            );
            entry.mechanicOverrides = ConvertCatalogMapToList(
                registry.GetKvpArray(entry.mechanicName, "MechanicOverrides")
            );
            entry.incompatibleWith = registry.GetIncompatibleWith(entry.mechanicName);
        }

        private static List<Dictionary<string, object>> ConvertCatalogMapToList(
            Dictionary<string, object> map
        )
        {
            if (map == null || map.Count == 0)
                return null;

            var list = new List<Dictionary<string, object>>(map.Count);
            foreach (var kv in map)
            {
                var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    [kv.Key] = kv.Value,
                };
                list.Add(entry);
            }
            return list;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null)
                return string.Empty;

            var stack = new Stack<string>();
            var current = go.transform;
            int guard = 0;
            while (current != null && guard < 64)
            {
                stack.Push(current.name ?? string.Empty);
                current = current.parent;
                guard++;
            }

            return string.Join("/", stack.ToArray());
        }

        private static Dictionary<string, object> ConvertSettingsForDump(
            (string key, object value)[] settings
        )
        {
            if (settings == null || settings.Length == 0)
                return null;

            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < settings.Length; i++)
            {
                var (key, value) = settings[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                dict[key] = ConvertValueForDump(value);
            }

            return dict.Count > 0 ? dict : null;
        }

        private static object ConvertValueForDump(object value)
        {
            if (value == null)
                return null;

            switch (value)
            {
                case string s:
                    return s;
                case bool b:
                    return b;
                case char ch:
                    return ch.ToString();
                case Enum e:
                    return e.ToString();
                case Color color:
                    return new Dictionary<string, object>
                    {
                        ["r"] = color.r,
                        ["g"] = color.g,
                        ["b"] = color.b,
                        ["a"] = color.a,
                    };
                case Vector2 v2:
                    return new Dictionary<string, object> { ["x"] = v2.x, ["y"] = v2.y };
                case Vector3 v3:
                    return new Dictionary<string, object>
                    {
                        ["x"] = v3.x,
                        ["y"] = v3.y,
                        ["z"] = v3.z,
                    };
                case Vector4 v4:
                    return new Dictionary<string, object>
                    {
                        ["x"] = v4.x,
                        ["y"] = v4.y,
                        ["z"] = v4.z,
                        ["w"] = v4.w,
                    };
                case Vector2Int v2i:
                    return new Dictionary<string, object> { ["x"] = v2i.x, ["y"] = v2i.y };
                case Vector3Int v3i:
                    return new Dictionary<string, object>
                    {
                        ["x"] = v3i.x,
                        ["y"] = v3i.y,
                        ["z"] = v3i.z,
                    };
                case Quaternion q:
                    return new Dictionary<string, object>
                    {
                        ["x"] = q.x,
                        ["y"] = q.y,
                        ["z"] = q.z,
                        ["w"] = q.w,
                    };
                case Rect rect:
                    return new Dictionary<string, object>
                    {
                        ["x"] = rect.x,
                        ["y"] = rect.y,
                        ["width"] = rect.width,
                        ["height"] = rect.height,
                    };
                case IDictionary dictionary:
                    var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        string key = entry.Key?.ToString();
                        if (string.IsNullOrWhiteSpace(key))
                            continue;
                        map[key] = ConvertValueForDump(entry.Value);
                    }
                    return map;
                case IEnumerable enumerable when value is not string:
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(ConvertValueForDump(item));
                    return list;
                case UnityEngine.Object unityObject:
                    return unityObject != null ? unityObject.name : null;
            }

            if (
                value is sbyte
                || value is byte
                || value is short
                || value is ushort
                || value is int
                || value is uint
                || value is long
                || value is ulong
                || value is float
                || value is double
                || value is decimal
            )
            {
                return value;
            }

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private void TryWriteMechanicDump(
            GameObject root,
            ItemInstruction instruction,
            string baseName
        )
        {
            if (root == null)
                return;

            List<MechanicDumpEntry> entries;
            if (_activeDumpContext != null)
                entries = new List<MechanicDumpEntry>(_activeDumpContext.Entries);
            else
                entries = new List<MechanicDumpEntry>();

            EnsureInstructionMechanics(entries, instruction, root);

            if (entries.Count == 0 && instruction != null)
            {
                Debug.LogWarning(
                    "[ProcederalItemGenerator] No mechanics registered during generation; dumping instruction-only payload.",
                    this
                );
                var fallback = BuildMechanicDumpEntry(
                    instruction.primary,
                    root,
                    null,
                    Array.Empty<(string key, object value)>(),
                    fromInstruction: true
                );
                if (fallback != null)
                {
                    fallback.mechanicType = "InstructionOnly";
                    fallback.componentShortName = string.Empty;
                    fallback.componentAssemblyQualifiedName = string.Empty;
                    fallback.appliedSettings ??= new Dictionary<string, object>(
                        StringComparer.OrdinalIgnoreCase
                    );
                    fallback.appliedSettings["source"] = "instruction";
                    entries.Add(fallback);
                }
            }

            string fileName = baseName + ".json";
            string tempRoot = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "_TemporaryFiles"))
                : Application.temporaryCachePath;
            if (string.IsNullOrWhiteSpace(tempRoot))
                tempRoot = Path.GetTempPath();

            try
            {
                Directory.CreateDirectory(tempRoot);
                string path = Path.Combine(tempRoot, fileName);
                string json = BuildMechanicDumpJson(root, instruction, entries);
                File.WriteAllText(path, json);
                Debug.Log(
                    $"[ProcederalItemGenerator] Wrote mechanic dump '{path}' (base={baseName}).",
                    this
                );

                if (deleteMechanicDumpOnShutdown)
                    TrackMechanicDumpForCleanup(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ProcederalItemGenerator] Failed to write mechanic dump: {ex.Message}",
                    this
                );
            }
        }

        private void EnsureInstructionMechanics(
            List<MechanicDumpEntry> entries,
            ItemInstruction instruction,
            GameObject root
        )
        {
            if (entries == null || instruction == null)
                return;

            EnsureInstructionMechanic(entries, instruction.primary, root, true);

            if (instruction.secondary == null || instruction.secondary.Count == 0)
                return;

            for (int i = 0; i < instruction.secondary.Count; i++)
            {
                EnsureInstructionMechanic(entries, instruction.secondary[i], root, true);
            }
        }

        private void EnsureInstructionMechanic(
            List<MechanicDumpEntry> entries,
            string mechanicName,
            GameObject root,
            bool fromInstruction
        )
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                if (
                    string.Equals(
                        entries[i].mechanicName,
                        mechanicName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return;
                }
            }

            var entry = BuildMechanicDumpEntry(
                mechanicName,
                root,
                null,
                Array.Empty<(string key, object value)>(),
                fromInstruction
            );
            if (entry != null)
                entries.Add(entry);
        }

        private string BuildMechanicDumpJson(
            GameObject root,
            ItemInstruction instruction,
            List<MechanicDumpEntry> entries
        )
        {
            var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["generatedAtUtc"] = DateTime.UtcNow.ToString("o"),
                ["generatorName"] = name,
                ["rootName"] = root != null ? root.name : string.Empty,
                ["primary"] =
                    instruction != null ? instruction.primary ?? string.Empty : string.Empty,
                ["secondary"] =
                    instruction != null
                    && instruction.secondary != null
                    && instruction.secondary.Count > 0
                        ? instruction.secondary.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                        : Array.Empty<string>(),
                ["mechanics"] = entries.Select(ConvertEntryForDump).ToList(),
            };

            return SerializeJson(payload);
        }

        private static List<Dictionary<string, object>> CloneDictionaryList(
            List<Dictionary<string, object>> source
        )
        {
            if (source == null || source.Count == 0)
                return new List<Dictionary<string, object>>();

            var list = new List<Dictionary<string, object>>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null || item.Count == 0)
                {
                    list.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
                    continue;
                }

                var copy = new Dictionary<string, object>(
                    item.Count,
                    StringComparer.OrdinalIgnoreCase
                );
                foreach (var kv in item)
                    copy[kv.Key] = kv.Value;
                list.Add(copy);
            }
            return list;
        }

        private static List<string> CloneStringList(List<string> source)
        {
            return source != null && source.Count > 0
                ? new List<string>(source)
                : new List<string>();
        }

        private static Dictionary<string, object> ConvertEntryForDump(MechanicDumpEntry entry)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["MechanicName"] = entry.mechanicName ?? string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(entry.mechanicCategory))
                result["MechanicCategory"] = entry.mechanicCategory;
            if (!string.IsNullOrWhiteSpace(entry.mechanicPath))
                result["MechanicPath"] = entry.mechanicPath;
            if (!string.IsNullOrWhiteSpace(entry.mechanicAttribute))
                result["MechanicAttribute"] = entry.mechanicAttribute;
            if (!string.IsNullOrWhiteSpace(entry.mechanicType))
                result["ComponentType"] = entry.mechanicType;
            if (!string.IsNullOrWhiteSpace(entry.componentShortName))
                result["ComponentShortName"] = entry.componentShortName;
            if (!string.IsNullOrWhiteSpace(entry.componentAssemblyQualifiedName))
                result["ComponentAssemblyQualifiedName"] = entry.componentAssemblyQualifiedName;
            if (!string.IsNullOrWhiteSpace(entry.gameObject))
                result["GameObject"] = entry.gameObject;

            result["Generator"] = CloneDictionaryList(entry.generator);
            result["Properties"] = CloneDictionaryList(entry.properties);
            result["Overrides"] = CloneDictionaryList(entry.overrides);
            result["MechanicOverrides"] = CloneDictionaryList(entry.mechanicOverrides);
            result["IncompatibleWith"] = CloneStringList(entry.incompatibleWith);
            if (entry.appliedSettings != null && entry.appliedSettings.Count > 0)
                result["AppliedSettings"] = entry.appliedSettings;
            if (entry.fromInstruction)
                result["Source"] = "Instruction";

            return result;
        }

        private static string SerializeJson(object value)
        {
            var sb = new StringBuilder(4096);
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int indentLevel)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    AppendQuotedString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case IDictionary dictionary:
                    WriteObject(sb, GetDictionaryEntries(dictionary), indentLevel);
                    return;
                case IEnumerable<KeyValuePair<string, object>> kvps:
                    WriteObject(sb, kvps, indentLevel);
                    return;
                case IEnumerable enumerable when value is not string:
                    WriteArray(sb, enumerable, indentLevel);
                    return;
                case IFormattable formattable:
                    sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                    return;
            }

            AppendQuotedString(sb, value.ToString());
        }

        private static void WriteObject(
            StringBuilder sb,
            IEnumerable<KeyValuePair<string, object>> entries,
            int indentLevel
        )
        {
            sb.Append("{\n");
            var list = entries.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)).ToList();

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                WriteIndent(sb, indentLevel + 1);
                AppendQuotedString(sb, entry.Key);
                sb.Append(": ");
                WriteValue(sb, entry.Value, indentLevel + 1);
                if (i < list.Count - 1)
                    sb.Append(',');
                sb.Append('\n');
            }

            WriteIndent(sb, indentLevel);
            sb.Append('}');
        }

        private static IEnumerable<KeyValuePair<string, object>> GetDictionaryEntries(
            IDictionary dictionary
        )
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                yield return new KeyValuePair<string, object>(key, entry.Value);
            }
        }

        private static void WriteArray(StringBuilder sb, IEnumerable enumerable, int indentLevel)
        {
            sb.Append("[\n");
            var items = new List<object>();
            foreach (var item in enumerable)
                items.Add(item);

            for (int i = 0; i < items.Count; i++)
            {
                WriteIndent(sb, indentLevel + 1);
                WriteValue(sb, items[i], indentLevel + 1);
                if (i < items.Count - 1)
                    sb.Append(',');
                sb.Append('\n');
            }

            WriteIndent(sb, indentLevel);
            sb.Append(']');
        }

        private static void WriteIndent(StringBuilder sb, int indentLevel)
        {
            for (int i = 0; i < indentLevel; i++)
                sb.Append("    ");
        }

        private static void AppendQuotedString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    switch (c)
                    {
                        case '\\':
                        case '"':
                            sb.Append('\\');
                            sb.Append(c);
                            break;
                        case '\b':
                            sb.Append("\\b");
                            break;
                        case '\f':
                            sb.Append("\\f");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (char.IsControl(c))
                            {
                                sb.Append("\\u");
                                sb.Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                sb.Append(c);
                            }

                            break;
                    }
                }
            }

            sb.Append('"');
        }

        private string BuildMechanicDumpBaseName(ItemInstruction instruction)
        {
            var parts = new List<string>();
            if (instruction != null)
            {
                if (!string.IsNullOrWhiteSpace(instruction.primary))
                    parts.Add(instruction.primary);
                if (instruction.secondary != null && instruction.secondary.Count > 0)
                {
                    for (int i = 0; i < instruction.secondary.Count; i++)
                    {
                        string sec = instruction.secondary[i];
                        if (!string.IsNullOrWhiteSpace(sec))
                            parts.Add(sec);
                    }
                }
            }

            var sanitized = parts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(SanitizeForFileName)
                .ToList();
            if (sanitized.Count == 0)
                sanitized.Add("GeneratedItem");

            string unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            sanitized.Add(unique);
            return string.Join("_", sanitized);
        }

        private static string SanitizeForFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "item";

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == '-' || c == '_')
                {
                    sb.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    sb.Append('_');
                }
            }

            if (sb.Length == 0)
                sb.Append("item");
            return sb.ToString();
        }

        private void TrackMechanicDumpForCleanup(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            lock (_mechanicDumpLock)
            {
                if (!_mechanicDumpFiles.Contains(path))
                    _mechanicDumpFiles.Add(path);

                if (!_mechanicDumpCleanupRegistered)
                {
                    Application.quitting += CleanupMechanicDumpFiles;
                    GameOverController.OnCountdownFinished += CleanupMechanicDumpFiles;
                    _mechanicDumpCleanupRegistered = true;
                }
            }
        }

        private static void CleanupMechanicDumpFiles()
        {
            lock (_mechanicDumpLock)
            {
                if (_mechanicDumpFiles.Count == 0)
                    return;

                for (int i = _mechanicDumpFiles.Count - 1; i >= 0; i--)
                {
                    string path = _mechanicDumpFiles[i];
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[ProcederalItemGenerator] Failed to delete mechanic dump '{path}': {ex.Message}"
                        );
                    }
                }

                _mechanicDumpFiles.Clear();
            }
        }

        // TrySet no longer needed here; reflection handled centrally via MechanicReflection

        public void Log(string msg)
        {
            if (debugLogs)
                Debug.Log($"[ProcederalItemGenerator] {msg}", this);
        }

        private sealed class MechanicDumpContext
        {
            public readonly List<MechanicDumpEntry> Entries = new List<MechanicDumpEntry>();

            public void AddOrMerge(MechanicDumpEntry incoming)
            {
                if (incoming == null)
                    return;

                var existing = Find(incoming.mechanicName, incoming.gameObject);
                if (existing == null)
                {
                    Entries.Add(incoming);
                    return;
                }

                if (string.IsNullOrWhiteSpace(existing.mechanicType))
                    existing.mechanicType = incoming.mechanicType;
                if (string.IsNullOrWhiteSpace(existing.componentShortName))
                    existing.componentShortName = incoming.componentShortName;
                if (string.IsNullOrWhiteSpace(existing.componentAssemblyQualifiedName))
                {
                    existing.componentAssemblyQualifiedName =
                        incoming.componentAssemblyQualifiedName;
                }
                if (string.IsNullOrWhiteSpace(existing.mechanicCategory))
                    existing.mechanicCategory = incoming.mechanicCategory;
                if (string.IsNullOrWhiteSpace(existing.mechanicAttribute))
                    existing.mechanicAttribute = incoming.mechanicAttribute;
                if (string.IsNullOrWhiteSpace(existing.mechanicPath))
                    existing.mechanicPath = incoming.mechanicPath;

                existing.generator = MergeList(existing.generator, incoming.generator);
                existing.properties = MergeList(existing.properties, incoming.properties);
                existing.overrides = MergeList(existing.overrides, incoming.overrides);
                existing.mechanicOverrides = MergeList(
                    existing.mechanicOverrides,
                    incoming.mechanicOverrides
                );
                existing.incompatibleWith = MergeStringList(
                    existing.incompatibleWith,
                    incoming.incompatibleWith
                );
                existing.appliedSettings = MergeDictionary(
                    existing.appliedSettings,
                    incoming.appliedSettings
                );
                existing.fromInstruction |= incoming.fromInstruction;
            }

            private MechanicDumpEntry Find(string mechanicName, string gameObjectPath)
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    var entry = Entries[i];
                    if (
                        string.Equals(
                            entry.mechanicName,
                            mechanicName,
                            StringComparison.OrdinalIgnoreCase
                        )
                        && string.Equals(entry.gameObject, gameObjectPath, StringComparison.Ordinal)
                    )
                    {
                        return entry;
                    }
                }

                return null;
            }

            private static Dictionary<string, object> MergeDictionary(
                Dictionary<string, object> existing,
                Dictionary<string, object> incoming
            )
            {
                if (incoming == null || incoming.Count == 0)
                    return existing;
                if (existing == null)
                {
                    return new Dictionary<string, object>(
                        incoming,
                        StringComparer.OrdinalIgnoreCase
                    );
                }
                foreach (var kv in incoming)
                    existing[kv.Key] = kv.Value;
                return existing;
            }

            private static List<Dictionary<string, object>> MergeList(
                List<Dictionary<string, object>> existing,
                List<Dictionary<string, object>> incoming
            )
            {
                if (incoming == null || incoming.Count == 0)
                    return existing;
                if (existing == null)
                {
                    return incoming != null
                        ? new List<Dictionary<string, object>>(incoming)
                        : new List<Dictionary<string, object>>();
                }
                existing.AddRange(incoming);
                return existing;
            }

            private static List<string> MergeStringList(
                List<string> existing,
                List<string> incoming
            )
            {
                if (incoming == null || incoming.Count == 0)
                    return existing;
                if (existing == null)
                {
                    return new List<string>(incoming);
                }
                foreach (var item in incoming)
                {
                    if (!existing.Contains(item))
                        existing.Add(item);
                }
                return existing;
            }
        }

        private sealed class MechanicDumpEntry
        {
            public string mechanicName;
            public string mechanicType;
            public string componentShortName;
            public string componentAssemblyQualifiedName;
            public string mechanicCategory;
            public string mechanicAttribute;
            public string mechanicPath;
            public string gameObject;
            public List<Dictionary<string, object>> generator;
            public List<Dictionary<string, object>> properties;
            public List<Dictionary<string, object>> overrides;
            public List<Dictionary<string, object>> mechanicOverrides;
            public List<string> incompatibleWith;
            public Dictionary<string, object> appliedSettings;
            public bool fromInstruction;
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
                    float ResolveFloat(object raw, float fallback)
                    {
                        switch (raw)
                        {
                            case float f:
                                return f;
                            case int i:
                                return i;
                            case long l:
                                return l;
                            case double d:
                                return (float)d;
                            case string s when float.TryParse(s, out var parsed):
                                return parsed;
                            default:
                                return fallback;
                        }
                    }

                    float current = 90f;
                    if (dict.TryGetValue("speed", out var speedVal))
                        current = ResolveFloat(speedVal, current);
                    if (dict.TryGetValue("angularSpeedDeg", out var ang))
                        current = ResolveFloat(ang, current);
                    string tok = ds.Trim().ToLowerInvariant();
                    if (tok == "clockwise")
                        current = -Mathf.Abs(current);
                    else
                        current = Mathf.Abs(current);
                    dict["angularSpeedDeg"] = current;
                    dict["speed"] = current;
                    dict.Remove("direction");
                }
            }
        }
    }
}
