using System;
using System.Collections.Generic;
using Game.Procederal.Api;
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

    public class ProcederalItemGenerator : MonoBehaviour
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

        private static bool TryParseColor(object raw, out Color color)
        {
            color = Color.white;
            if (raw is string s)
                return ColorUtils.TryParse(s, out color);
            return false;
        }

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
            "Primary Mechanic List JSON (TextAsset). If null, will try Resources.Load('Primary Mechanic List')"
        )]
        public TextAsset primaryMechanicListJson;

        [Tooltip(
            "Modifier Mechanic List JSON (TextAsset). If null, will try Resources.Load('Modifier Mechanic List')"
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

            switch (instruction.GetPrimaryKind())
            {
                case MechanicKind.Projectile:
                    BuildProjectileSet(root, instruction, p, subItems);
                    break;
                case MechanicKind.Aura:
                    BuildAura(root, instruction, p, subItems);
                    break;
                case MechanicKind.Beam:
                    BuildBeam(root, instruction, p, subItems);
                    break;
                case MechanicKind.Strike:
                    BuildStrike(root, instruction, p, subItems);
                    break;
                case MechanicKind.Whip:
                    BuildWhip(root, instruction, p, subItems);
                    break;
                case MechanicKind.Ripple:
                    BuildRipple(root, instruction, p, subItems);
                    break;
                case MechanicKind.SwordSlash:
                    BuildSwordSlash(root, instruction, p, subItems);
                    break;
                default:
                    Log($"Unsupported primary mechanic '{instruction.primary}'.");
                    break;
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

        private void BuildRipple(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            // Pull Ripple defaults from JSON
            var json = LoadAndMergeJsonSettings("Ripple");

            // Settings
            float startRadius = 1f;
            if (json.TryGetValue("startRadius", out var sr))
            {
                if (sr is float fsr)
                    startRadius = Mathf.Max(0f, fsr);
                else if (sr is int isr)
                    startRadius = Mathf.Max(0f, isr);
            }
            float endDiameter = 8f;
            if (json.TryGetValue("endDiameter", out var ed))
            {
                if (ed is float fed)
                    endDiameter = Mathf.Max(0.01f, fed);
                else if (ed is int ied)
                    endDiameter = Mathf.Max(0.01f, ied);
            }
            float growDuration = 1.5f;
            if (json.TryGetValue("growDuration", out var gd))
            {
                if (gd is float fgd)
                    growDuration = Mathf.Max(0.01f, fgd);
                else if (gd is int igd)
                    growDuration = Mathf.Max(0.01f, igd);
            }
            float edgeThickness = 0.2f;
            if (json.TryGetValue("edgeThickness", out var et))
            {
                if (et is float fet)
                    edgeThickness = Mathf.Max(0.01f, fet);
                else if (et is int iet)
                    edgeThickness = Mathf.Max(0.01f, iet);
            }
            int damage = 5;
            if (json.TryGetValue("damage", out var dmg))
            {
                if (dmg is int id)
                    damage = id;
                else if (dmg is float fd)
                    damage = Mathf.RoundToInt(fd);
            }
            bool showViz = true;
            if (json.TryGetValue("showVisualization", out var sv))
            {
                if (sv is bool b)
                    showViz = b;
                else if (sv is string s && bool.TryParse(s, out var pb))
                    showViz = pb;
            }
            Color vizColor = Color.cyan;
            if (json.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (TryParseColor(sc, out c))
                    vizColor = c;
            }
            bool spawnOnInterval = false;
            if (json.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            if (
                json.TryGetValue("numberOfItemsToSpawn", out var nts)
                || json.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float interval = 0.5f;
            if (json.TryGetValue("interval", out var iv))
            {
                if (iv is float fiv)
                    interval = Mathf.Max(0.01f, fiv);
                else if (iv is int iiv)
                    interval = Mathf.Max(0.01f, iiv);
                else if (iv is string siv && float.TryParse(siv, out var pf))
                    interval = Mathf.Max(0.01f, pf);
            }

            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<Game.Procederal.Api.RippleIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = owner != null ? owner : transform;
                spawner.interval = interval;
                // Respect generator-level override when provided
                spawner.countPerInterval = Mathf.Max(
                    1,
                    generatorChildrenCount > 0 ? generatorChildrenCount : numberToSpawn
                );
                spawner.startRadius = startRadius;
                spawner.endDiameter = endDiameter;
                spawner.growDuration = growDuration;
                spawner.edgeThickness = edgeThickness;
                spawner.damage = damage;
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.showVisualization = showViz;
                spawner.vizColor = vizColor;
                spawner.debugLogs = p.debugLogs || debugLogs;

                // Auto-apply compatible instruction modifiers to spawned ripples
                if (autoApplyCompatibleModifiers)
                {
                    foreach (var mk in GetModifiersToApply(instruction))
                    {
                        switch (mk)
                        {
                            case MechanicKind.RippleOnHit:
                                spawner.AddModifierSpec(
                                    "RippleOnHit",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Drain:
                                // Ensure the owner has Drain so ripple damage can report to it
                                EnsureOwnerDrain(
                                    spawner.owner != null ? spawner.owner : transform,
                                    p
                                );
                                break;
                            // Other modifiers like Explosion/DoT aren't wired into RippleMechanic yet; skip for now
                        }
                    }
                }
                return;
            }

            // One-shot ripple instance
            var go = new GameObject("Ripple");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            AddMechanicByName(
                go,
                "Ripple",
                new (string key, object val)[]
                {
                    ("startRadius", startRadius),
                    ("endDiameter", endDiameter),
                    ("growDuration", growDuration),
                    ("edgeThickness", edgeThickness),
                    ("damage", damage),
                    ("excludeOwner", true),
                    ("requireMobTag", true),
                    ("showVisualization", showViz),
                    ("vizColor", vizColor),
                    ("debugLogs", p.debugLogs || debugLogs),
                }
            );
            InitializeMechanics(go, owner, target);
            subItems.Add(go);
        }

        // Sword Slash: emits a series of crescent outlines that travel forward, keeping their initial size.
        // Implemented by composing a static SwordSlashPayload (crescent collider/line) with a ProjectileMechanic for motion.
        private void BuildSwordSlash(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            // JSON defaults (if present)
            var json = LoadAndMergeJsonSettings("SwordSlash");
            int seriesCount = 3;
            float intervalBetween = 0.08f;
            bool spawnOnInterval = false;
            float seriesInterval = 0.8f;
            float outerRadius = 1.5f;
            float width = 0.5f;
            float arcLen = 120f;
            bool edgeOnly = true;
            float edgeThickness = 0.2f;
            int damage = 8;
            float speed = 12f;
            Color color = Color.white;

            ReadIf(json, "seriesCount", ref seriesCount);
            ReadIf(json, "intervalBetween", ref intervalBetween);
            ReadIf(json, "spawnOnInterval", ref spawnOnInterval);
            ReadIf(json, "interval", ref seriesInterval);
            ReadIf(json, "outerRadius", ref outerRadius);
            ReadIf(json, "width", ref width);
            ReadIf(json, "arcLengthDeg", ref arcLen);
            ReadIf(json, "edgeOnly", ref edgeOnly);
            ReadIf(json, "edgeThickness", ref edgeThickness);
            ReadIf(json, "damage", ref damage);
            ReadIf(json, "speed", ref speed);
            if (json.TryGetValue("spriteColor", out var sc))
                TryParseColor(sc, out color);

            // If interval-based spawning is requested, attach a spawner and route all creation through it
            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<Game.Procederal.Api.SwordSlashIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = owner != null ? owner : transform;
                spawner.interval = Mathf.Max(0.01f, seriesInterval);
                spawner.seriesCount = Mathf.Max(1, seriesCount);
                spawner.intervalBetween = Mathf.Max(0f, intervalBetween);
                spawner.outerRadius = Mathf.Max(0.0001f, outerRadius);
                spawner.width = Mathf.Max(0.0001f, width);
                spawner.arcLengthDeg = Mathf.Clamp(arcLen, 1f, 359f);
                spawner.edgeOnly = edgeOnly;
                spawner.edgeThickness = Mathf.Max(0.0001f, edgeThickness);
                spawner.vizColor = color;
                spawner.damage = Mathf.Max(0, damage);
                spawner.speed = Mathf.Max(0.01f, speed);
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                // If Track is applied, aim each burst toward nearest enemy
                spawner.aimAtNearestEnemy = instruction.HasSecondary(MechanicKind.Track);
                spawner.debugLogs = p.debugLogs || debugLogs;

                // Propagate compatible modifiers to spawned slashes
                if (autoApplyCompatibleModifiers)
                {
                    foreach (var mk in GetModifiersToApply(instruction))
                    {
                        switch (mk)
                        {
                            case MechanicKind.Drain:
                                EnsureOwnerDrain(
                                    spawner.owner != null ? spawner.owner : transform,
                                    p
                                );
                                spawner.AddModifierSpec(
                                    "Drain",
                                    ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio)),
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Lock:
                                spawner.AddModifierSpec("Lock");
                                break;
                            case MechanicKind.DamageOverTime:
                                spawner.AddModifierSpec(
                                    "DamageOverTime",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Bounce:
                                spawner.AddModifierSpec("Bounce");
                                break;
                            case MechanicKind.Explosion:
                                spawner.AddModifierSpec(
                                    "Explosion",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.RippleOnHit:
                                spawner.AddModifierSpec(
                                    "RippleOnHit",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Track:
                                // Skip adding Track mechanic directly; handled by aimAtNearestEnemy
                                break;
                        }
                    }
                }
                return;
            }

            // Direction: player movement by default
            Vector2 dir = Vector2.right;
            var ownerT = owner != null ? owner : transform;
            var ownerRb = ownerT != null ? ownerT.GetComponent<Rigidbody2D>() : null;
            if (ownerRb != null && ownerRb.linearVelocity.sqrMagnitude > 0.01f)
                dir = ownerRb.linearVelocity.normalized;

            // If Track modifier is applied, spawn facing nearest enemy
            if (instruction.HasSecondary(MechanicKind.Track))
            {
                var nearest = FindNearestMob(ownerT);
                if (nearest != null)
                {
                    Vector2 to = (Vector2)(nearest.position - ownerT.position);
                    if (to.sqrMagnitude > 1e-6f)
                        dir = to.normalized;
                }
            }

            // Spawn a series of slashes offset in space so they appear as a moving train
            int count = Mathf.Max(1, seriesCount);
            float spacing = Mathf.Max(0f, intervalBetween) * Mathf.Max(0.01f, speed);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"SwordSlash_{i}");
                go.transform.SetParent(root.transform, false);
                // World placement relative to owner so series starts stretched behind and moves forward
                if (ownerT != null)
                    go.transform.position = ownerT.position - (Vector3)(dir * spacing * i);
                else
                    go.transform.localPosition = Vector3.zero;
                go.layer = ownerT != null ? ownerT.gameObject.layer : go.layer;

                // Add payload (static crescent geometry)
                var payload = go.AddComponent<Mechanics.Neuteral.SwordSlashPayload>();
                payload.outerRadius = outerRadius;
                payload.width = width;
                payload.arcLengthDeg = arcLen;
                payload.edgeOnly = edgeOnly;
                payload.edgeThickness = edgeThickness;
                payload.showVisualization = true;
                payload.vizColor = color;

                // Orient the crescent so its forward (local +X) matches the travel direction
                go.transform.right = dir;

                // Motion and damage
                AddMechanicByName(
                    go,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("direction", dir),
                        ("speed", speed),
                        ("damage", damage),
                        ("requireMobTag", true),
                        ("excludeOwner", true),
                        ("destroyOnHit", true),
                    }
                );

                // Rigidbody for reliable trigger events and physics
                var rb = go.GetComponent<Rigidbody2D>();
                if (rb == null)
                    rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                InitializeMechanics(go, owner, target);

                // Safety lifetime
                go.AddComponent<_AutoDestroyAfterSeconds>().seconds = 4f;

                subItems.Add(go);
            }
        }

        private static Transform FindNearestMob(Transform from)
        {
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
                return null;
            float best = float.MaxValue;
            Transform bestT = null;
            foreach (var go in mobs)
            {
                if (go == null)
                    continue;
                float d2 = (go.transform.position - from.position).sqrMagnitude;
                if (d2 < best)
                {
                    best = d2;
                    bestT = go.transform;
                }
            }
            return bestT;
        }

        // no delayed activation helper needed; we space instances in world instead

        private void BuildWhip(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            // Up to 4 whips in cardinal directions
            int desired = p != null ? p.subItemCount : 1;
            if (generatorChildrenCount > 0)
                desired = generatorChildrenCount;
            int count = Mathf.Clamp(Mathf.Max(1, desired), 1, 4);
            string[] dirs = new[] { "right", "up", "left", "down" };
            // Attach a controller to allow runtime spawning of more arcs
            var set = root.AddComponent<Game.Procederal.Api.WhipArcSet>();
            set.generator = this;
            set.owner = owner != null ? owner : transform;
            set.target = target;
            set.debugLogs = p.debugLogs || debugLogs;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Whip_{i}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;

                var json = LoadAndMergeJsonSettings("Whip");
                var settings = new List<(string key, object val)>();
                // Apply JSON props if present
                if (json.TryGetValue("outerRadius", out var or))
                    settings.Add(("outerRadius", or));
                if (json.TryGetValue("width", out var w))
                    settings.Add(("width", w));
                if (json.TryGetValue("arcLengthDeg", out var ad))
                    settings.Add(("arcLengthDeg", ad));
                if (json.TryGetValue("drawDuration", out var dd))
                    settings.Add(("drawDuration", dd));
                if (json.TryGetValue("interval", out var iv))
                    settings.Add(("interval", iv));
                if (json.TryGetValue("damagePerInterval", out var dmg))
                    settings.Add(("damagePerInterval", dmg));
                if (json.TryGetValue("showVisualization", out var sv))
                    settings.Add(("showVisualization", sv));
                if (json.TryGetValue("spriteColor", out var sc))
                    settings.Add(("vizColor", sc));

                // Direction per index
                settings.Add(("direction", dirs[i % dirs.Length]));
                settings.Add(("excludeOwner", true));
                settings.Add(("requireMobTag", true));
                settings.Add(("debugLogs", p.debugLogs || debugLogs));

                AddMechanicByName(go, "Whip", settings.ToArray());
                InitializeMechanics(go, owner, target);
                subItems.Add(go);
            }
            set.RefreshDirs();
        }

        private void BuildProjectileSet(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            int desired = p != null ? p.subItemCount : 1;
            if (generatorChildrenCount > 0)
                desired = generatorChildrenCount;
            int count = Mathf.Max(1, desired);
            float baseAngle = p.startAngleDeg;
            bool wantOrbit = instruction.HasSecondary(MechanicKind.Orbit);
            // Visual defaults from JSON (allows spriteType/spriteColor overrides)
            var projectileJson = LoadAndMergeJsonSettings("Projectile");
            string spriteType = null;
            if (projectileJson.TryGetValue("spriteType", out var st))
                spriteType = (st as string)?.Trim();
            if (string.IsNullOrEmpty(spriteType))
                spriteType = "circle";
            string customPath = null;
            if (projectileJson.TryGetValue("customSpritePath", out var cp))
                customPath = (cp as string)?.Trim();
            Color projColor = Color.white;
            if (projectileJson.TryGetValue("spriteColor", out var sc))
            {
                if (!TryParseColor(sc, out projColor))
                    projColor = Color.white;
            }

            // Spawn behavior selection from JSON (e.g., neuteral | neutral | chaos | orbit)
            string spawnBehavior = null;
            if (projectileJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();

            // Projectile spawn behavior from JSON
            bool spawnOnInterval = false;
            if (projectileJson.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            // Accept both camelCase and original key
            if (
                projectileJson.TryGetValue("numberOfItemsToSpawn", out var nts)
                || projectileJson.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float spawnInterval = 0.5f;
            if (projectileJson.TryGetValue("interval", out var iv))
            {
                if (iv is float fiv)
                    spawnInterval = Mathf.Max(0.01f, fiv);
                else if (iv is int iiv)
                    spawnInterval = Mathf.Max(0.01f, iiv);
                else if (iv is string siv && float.TryParse(siv, out var pf))
                    spawnInterval = Mathf.Max(0.01f, pf);
            }
            float spawnRadius = 0f;
            if (projectileJson.TryGetValue("radius", out var rv))
            {
                if (rv is float fr)
                    spawnRadius = Mathf.Max(0f, fr);
                else if (rv is int ir)
                    spawnRadius = Mathf.Max(0f, ir);
                else if (rv is string sr && float.TryParse(sr, out var pr))
                    spawnRadius = Mathf.Max(0f, pr);
            }
            float lifetime = -1f;
            if (projectileJson.TryGetValue("lifetime", out var lt))
            {
                if (lt is float flt)
                    lifetime = Mathf.Max(0f, flt);
                else if (lt is int ilt)
                    lifetime = Mathf.Max(0f, ilt);
                else if (lt is string slt && float.TryParse(slt, out var plt))
                    lifetime = Mathf.Max(0f, plt);
            }
            float projSpeed = -1f;
            if (projectileJson.TryGetValue("speed", out var spd))
            {
                if (spd is float fs)
                    projSpeed = fs;
                else if (spd is int ispd)
                    projSpeed = ispd;
                else if (spd is string sspd && float.TryParse(sspd, out var pfs))
                    projSpeed = pfs;
            }

            // Orbit may override spawnOnInterval to false (see Modifier Mechanic List). Respect that.
            if (wantOrbit)
            {
                var orbitOverrides = LoadKvpArrayForMechanic("Orbit", "MechanicOverrides");
                if (orbitOverrides.TryGetValue("spawnOnInterval", out var oSoI))
                {
                    bool orbitSaysSpawn = false;
                    if (oSoI is bool ob)
                        orbitSaysSpawn = ob;
                    else if (oSoI is string os && bool.TryParse(os, out var pob))
                        orbitSaysSpawn = pob;
                    if (!orbitSaysSpawn)
                        spawnOnInterval = false;
                }
            }

            // If Orbit is selected (either via modifier or spawnBehavior override), route through OrbitSpawnBehavior
            bool routeOrbitBehavior = wantOrbit || string.Equals(spawnBehaviorNorm, "orbit");
            if (routeOrbitBehavior)
            {
                var orbitSpawner = root.AddComponent<Game.Procederal.Api.OrbitSpawnBehavior>();
                orbitSpawner.generator = this;
                orbitSpawner.owner = owner != null ? owner : transform;
                orbitSpawner.orbitRadius = p.orbitRadius;
                orbitSpawner.angularSpeedDeg = p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f;
                orbitSpawner.useInterval = spawnOnInterval;
                orbitSpawner.interval = spawnInterval;
                // Use generatorChildrenCount override when provided
                int orbitCount = generatorChildrenCount > 0 ? generatorChildrenCount : count;
                orbitSpawner.countPerInterval = Mathf.Max(1, orbitCount);
                orbitSpawner.spriteType = spriteType;
                orbitSpawner.customSpritePath = customPath;
                orbitSpawner.spriteColor = projColor;
                // Visual scale for orbit children can be overridden by Orbit JSON radius; defaults to projectileSize
                float orbitVisualScale = p.projectileSize;
                var orbitJson = LoadAndMergeJsonSettings("Orbit");
                if (orbitJson.TryGetValue("radius", out var or))
                {
                    if (or is float fo)
                        orbitVisualScale = Mathf.Max(0.0001f, fo);
                    else if (or is int io)
                        orbitVisualScale = Mathf.Max(0.0001f, io);
                    else if (or is string so && float.TryParse(so, out var po))
                        orbitVisualScale = Mathf.Max(0.0001f, po);
                }
                // If Orbit modifier specifies destroyOnHit override, apply it to child projectiles via flags
                if (orbitJson.TryGetValue("destroyOnHit", out var odh))
                {
                    bool val = false;
                    if (odh is bool bt)
                        val = bt;
                    else if (odh is string s && bool.TryParse(s, out var pb))
                        val = pb;
                    orbitSpawner.overrideDestroyOnHit = true;
                    orbitSpawner.destroyOnHit = val;
                    // If we explicitly disable destroyOnHit, also disable lifetime auto-destroy
                    if (!val)
                        orbitSpawner.lifetime = -1f;
                }
                orbitSpawner.childScale = orbitVisualScale;
                // Child projectile behavior
                orbitSpawner.damage = p.projectileDamage;
                // Do not force destroyOnHit here; allow Orbit MechanicOverrides to decide (set above) or child defaults
                orbitSpawner.excludeOwner = true;
                orbitSpawner.requireMobTag = true;
                orbitSpawner.lifetime = lifetime;
                orbitSpawner.debugLogs = p.debugLogs || debugLogs;

                // Ensure owner-level Drain if selected
                if (instruction.HasSecondary(MechanicKind.Drain))
                {
                    orbitSpawner.applyDrain = true;
                    orbitSpawner.drainLifeStealRatio = Mathf.Clamp01(p.lifeStealRatio);
                    Transform drainOwner =
                        orbitSpawner.owner != null ? orbitSpawner.owner : transform;
                    if (drainOwner != null)
                    {
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
                }

                // If not using interval, spawn the desired number now
                if (!orbitSpawner.useInterval)
                {
                    orbitSpawner.Spawn(Mathf.Max(1, orbitCount));
                }
                // subItems intentionally left empty; OrbitSpawnBehavior manages its children
                return;
            }

            // Non-orbit path: if JSON requests interval spawning for projectiles, attach a spawner and skip static creation
            if (spawnOnInterval)
            {
                var spawner = root.AddComponent<IntervalSpawner>();
                spawner.generator = this;
                spawner.owner = owner != null ? owner : transform;
                spawner.interval = spawnInterval;
                // Use generatorChildrenCount override when provided
                int intervalCount =
                    generatorChildrenCount > 0 ? generatorChildrenCount : numberToSpawn;
                spawner.countPerInterval = Mathf.Max(1, intervalCount);
                spawner.spawnRadius = Mathf.Max(0f, spawnRadius);
                spawner.lifetime = lifetime;
                spawner.spriteType = spriteType;
                spawner.customSpritePath = customPath;
                spawner.spriteColor = projColor;
                spawner.excludeOwner = true;
                spawner.requireMobTag = true;
                spawner.destroyOnHit = p.projectileDestroyOnHit;
                spawner.damage = p.projectileDamage;
                spawner.projectileSpeed = projSpeed > 0f ? projSpeed : -1f;
                spawner.debugLogs = p.debugLogs || debugLogs;
                // Respect spawnBehavior for resolver selection
                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<Game.Procederal.Api.ChaosSpawnPosition>()
                            ?? root.AddComponent<Game.Procederal.Api.ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<Game.Procederal.Api.NeutralSpawnPositon>()
                            ?? root.AddComponent<Game.Procederal.Api.NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                    // If set to 'orbit' but no orbit modifier, we keep IntervalSpawner; orbit behavior is handled above
                }
                // Auto-apply compatible instruction modifiers to spawned projectiles
                if (autoApplyCompatibleModifiers)
                {
                    foreach (var mk in GetModifiersToApply(instruction))
                    {
                        switch (mk)
                        {
                            case MechanicKind.Drain:
                                spawner.applyDrain = true;
                                spawner.drainLifeStealRatio = Mathf.Clamp01(p.lifeStealRatio);
                                spawner.AddModifierSpec(
                                    "Drain",
                                    ("lifeStealRatio", spawner.drainLifeStealRatio)
                                );
                                EnsureOwnerDrain(
                                    spawner.owner != null ? spawner.owner : transform,
                                    p
                                );
                                break;
                            case MechanicKind.Lock:
                                spawner.AddModifierSpec("Lock");
                                break;
                            case MechanicKind.Track:
                                spawner.AddModifierSpec("Track");
                                break;
                            case MechanicKind.DamageOverTime:
                                spawner.AddModifierSpec(
                                    "DamageOverTime",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Bounce:
                                spawner.AddModifierSpec("Bounce");
                                break;
                            case MechanicKind.Explosion:
                                spawner.AddModifierSpec(
                                    "Explosion",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.RippleOnHit:
                                spawner.AddModifierSpec(
                                    "RippleOnHit",
                                    ("debugLogs", p.debugLogs || debugLogs)
                                );
                                break;
                            case MechanicKind.Orbit:
                                // Skip: orbit behavior is managed by OrbitSpawnBehavior path
                                break;
                        }
                    }
                }
                // subItems intentionally left empty; spawner will create at runtime
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Projectile_{i}");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero; // center by default

                // Visual scale policy for size
                float visualScale = p.projectileSize;
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, visualScale);

                // Visual: SpriteRenderer using JSON-configured sprite type and color
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite chosen = null;
                switch ((spriteType ?? "circle").ToLowerInvariant())
                {
                    case "custom":
                        if (!string.IsNullOrEmpty(customPath))
                            chosen = Resources.Load<Sprite>(customPath);
                        if (chosen == null)
                            chosen = GetUnitCircleSprite();
                        break;
                    case "square":
                        chosen = GetUnitSquareSprite();
                        break;
                    case "circle":
                    default:
                        chosen = GetUnitCircleSprite();
                        break;
                }
                sr.sprite = chosen;
                sr.color = projColor;
                sr.sortingOrder = 0;

                // Collider (trigger)
                var cc = go.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 1f; // use radius=1 as requested; scale still affects world size

                // Physics body for velocity-based motion if ProjectileMechanic uses Rigidbody2D
                if (go.GetComponent<Rigidbody2D>() == null)
                {
                    var rb = go.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }

                // Mechanics (dynamic)
                AddMechanicByName(
                    go,
                    "Projectile",
                    new (string key, object val)[]
                    {
                        ("damage", p.projectileDamage),
                        ("destroyOnHit", p.projectileDestroyOnHit),
                        ("excludeOwner", true),
                        ("requireMobTag", true),
                        ("debugLogs", p.debugLogs || debugLogs),
                        // If orbiting, let Orbit drive movement
                        ("disableSelfSpeed", wantOrbit),
                    }
                );

                if (wantOrbit)
                {
                    AddMechanicByName(
                        go,
                        "Orbit",
                        new (string key, object val)[]
                        {
                            ("radius", p.orbitRadius),
                            ("angularSpeedDeg", p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f),
                            ("debugLogs", p.debugLogs || debugLogs),
                        }
                    );

                    // Even spacing via starting angle offset; place at angle if desired
                    float angle = baseAngle + (360f * i / count);
                    PlaceAtPolar(go.transform, angle, p.orbitRadius);
                }

                // Initialize MechanicContext if required
                InitializeMechanics(go, owner, target);

                subItems.Add(go);
            }
        }

        private void BuildAura(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var go = new GameObject("Aura");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero; // center on owner

            // Pull Aura defaults from JSON
            var auraJson = LoadAndMergeJsonSettings("Aura");
            // Read JSON radius/interval/damagePerInterval/showVisualization/vizColor
            float jRadius = 2f;
            if (auraJson.TryGetValue("radius", out var arv))
            {
                if (arv is float fr)
                    jRadius = fr;
                else if (arv is int ir)
                    jRadius = ir;
                else if (arv is string sr && float.TryParse(sr, out var rpf))
                    jRadius = rpf;
            }
            float jInterval = 0.5f;
            if (auraJson.TryGetValue("interval", out var aiv))
            {
                if (aiv is float fiv)
                    jInterval = fiv;
                else if (aiv is int iiv)
                    jInterval = iiv;
                else if (aiv is string siv && float.TryParse(siv, out var ipf))
                    jInterval = ipf;
            }
            int jDamage = 1;
            if (auraJson.TryGetValue("damagePerInterval", out var adv))
            {
                if (adv is int ai)
                    jDamage = ai;
                else if (adv is float af)
                    jDamage = Mathf.RoundToInt(af);
                else if (adv is string asv && int.TryParse(asv, out var ai2))
                    jDamage = ai2;
            }
            bool jShowViz = true;
            if (auraJson.TryGetValue("showVisualization", out var sv))
            {
                if (sv is bool sb)
                    jShowViz = sb;
                else if (sv is string ss && bool.TryParse(ss, out var sbb))
                    jShowViz = sbb;
            }
            Color auraVizColor = new Color(0f, 0f, 0f, 0.5f);
            if (auraJson.TryGetValue("spriteColor", out var asc))
            {
                Color c;
                if (TryParseColor(asc, out c))
                    auraVizColor = c;
            }

            // Apply JSON defaults first
            var auraSettings = new List<(string key, object val)>
            {
                ("radius", jRadius),
                ("interval", Mathf.Max(0.01f, jInterval)),
                ("damagePerInterval", Mathf.Max(0, jDamage)),
                ("showVisualization", jShowViz),
                ("vizColor", auraVizColor),
                ("debugLogs", p.debugLogs || debugLogs),
            };

            AddMechanicByName(go, "Aura", auraSettings.ToArray());

            // Keep root scale neutral; AuraMechanic handles collider/viz scaling internally.
            go.transform.localScale = Vector3.one;

            // Drain modifier (if present) will be applied via AddModifierToAll after primaries are built

            InitializeMechanics(go, owner, target);
            subItems.Add(go);
        }

        private void BuildBeam(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            // Pull Beam defaults from JSON
            var beamJson = LoadAndMergeJsonSettings("Beam");

            // Visual color if provided (for debug sprites)
            Color vizColor = Color.white;
            if (beamJson.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (TryParseColor(sc, out c))
                    vizColor = c;
            }

            // Build common Beam settings that apply to each instance
            var beamSettings = new List<(string key, object val)>();
            if (beamJson.TryGetValue("maxDistance", out var md))
                beamSettings.Add(("maxDistance", md));
            if (beamJson.TryGetValue("speed", out var sp))
                beamSettings.Add(("speed", sp));
            if (beamJson.TryGetValue("extendSpeed", out var es))
                beamSettings.Add(("extendSpeed", es));
            if (beamJson.TryGetValue("direction", out var dir))
                beamSettings.Add(("direction", dir));
            if (beamJson.TryGetValue("radius", out var br))
                beamSettings.Add(("radius", br)); // width alias handled by settings helper
            if (beamJson.TryGetValue("beamWidth", out var bw))
                beamSettings.Add(("beamWidth", bw));
            if (beamJson.TryGetValue("damagePerInterval", out var dpi))
                beamSettings.Add(("damagePerInterval", dpi));
            if (beamJson.TryGetValue("damage", out var dmg))
                beamSettings.Add(("damage", dmg));
            if (beamJson.TryGetValue("interval", out var iv))
                beamSettings.Add(("interval", iv));
            // Enforce default filters
            beamSettings.Add(("requireMobTag", true));
            beamSettings.Add(("excludeOwner", true));
            beamSettings.Add(("showVisualization", true));
            beamSettings.Add(("vizColor", vizColor));
            beamSettings.Add(("debugLogs", p.debugLogs || debugLogs));

            // Spawn behavior toggles (interval-based spawning)
            bool spawnOnInterval = false;
            if (beamJson.TryGetValue("spawnOnInterval", out var soi))
            {
                if (soi is bool b)
                    spawnOnInterval = b;
                else if (soi is string ss && bool.TryParse(ss, out var pb))
                    spawnOnInterval = pb;
            }
            int numberToSpawn = 1;
            if (
                beamJson.TryGetValue("numberOfItemsToSpawn", out var nts)
                || beamJson.TryGetValue("NumberOfItemsToSpawn", out nts)
            )
            {
                if (nts is int ni)
                    numberToSpawn = Mathf.Max(1, ni);
                else if (nts is float nf)
                    numberToSpawn = Mathf.Max(1, Mathf.RoundToInt(nf));
                else if (nts is string ns && int.TryParse(ns, out var nsi))
                    numberToSpawn = Mathf.Max(1, nsi);
            }
            float spawnInterval = 0.5f;
            if (beamJson.TryGetValue("interval", out var biv))
            {
                if (biv is float fiv)
                    spawnInterval = Mathf.Max(0.01f, fiv);
                else if (biv is int iiv)
                    spawnInterval = Mathf.Max(0.01f, iiv);
                else if (biv is string siv && float.TryParse(siv, out var pf))
                    spawnInterval = Mathf.Max(0.01f, pf);
            }
            // Spawn placement resolver selection
            string spawnBehavior = null;
            if (beamJson.TryGetValue("spawnBehavior", out var sbRaw))
                spawnBehavior = (sbRaw as string)?.Trim();
            string spawnBehaviorNorm = string.IsNullOrEmpty(spawnBehavior)
                ? null
                : spawnBehavior.ToLowerInvariant();
            float spawnRadius = 0f;
            if (beamJson.TryGetValue("spawnRadius", out var srv))
            {
                if (srv is float fr)
                    spawnRadius = Mathf.Max(0f, fr);
                else if (srv is int ir)
                    spawnRadius = Mathf.Max(0f, ir);
                else if (srv is string sr && float.TryParse(sr, out var pr))
                    spawnRadius = Mathf.Max(0f, pr);
            }

            if (spawnOnInterval)
            {
                // Attach a beam-specific interval spawner so the parent persists
                var spawner = root.AddComponent<BeamIntervalSpawner>();
                spawner.generator = this;
                spawner.owner = owner != null ? owner : transform;
                spawner.interval = spawnInterval;
                spawner.countPerInterval = Mathf.Max(1, numberToSpawn);
                spawner.debugLogs = p.debugLogs || debugLogs;

                // Choose resolver per spawnBehavior
                if (!string.IsNullOrEmpty(spawnBehaviorNorm))
                {
                    if (spawnBehaviorNorm == "chaos")
                    {
                        var chaos =
                            root.GetComponent<Game.Procederal.Api.ChaosSpawnPosition>()
                            ?? root.AddComponent<Game.Procederal.Api.ChaosSpawnPosition>();
                        spawner.spawnResolverBehaviour = chaos;
                    }
                    else if (spawnBehaviorNorm == "neuteral" || spawnBehaviorNorm == "neutral")
                    {
                        var neu =
                            root.GetComponent<Game.Procederal.Api.NeutralSpawnPositon>()
                            ?? root.AddComponent<Game.Procederal.Api.NeutralSpawnPositon>();
                        spawner.spawnResolverBehaviour = neu;
                    }
                }

                // Pass per-beam settings
                spawner.SetBeamSettings(beamSettings.ToArray());
                // Apply spawn radius on the spawner (resolver will receive it via TryGetSpawn)
                spawner.spawnRadius = spawnRadius;
                // Propagate DoT modifier to spawned beams if selected
                if (instruction.HasSecondary(MechanicKind.DamageOverTime))
                {
                    spawner.AddModifierSpec(
                        "DamageOverTime",
                        ("debugLogs", p.debugLogs || debugLogs)
                    );
                }
                // Propagate Bounce modifier to spawned beams if selected
                if (instruction.HasSecondary(MechanicKind.Bounce))
                {
                    spawner.AddModifierSpec("Bounce", ("debugLogs", p.debugLogs || debugLogs));
                }
                // Propagate Explosion modifier to spawned beams if selected
                if (instruction.HasSecondary(MechanicKind.Explosion))
                {
                    spawner.AddModifierSpec("Explosion", ("debugLogs", p.debugLogs || debugLogs));
                }
                // Propagate RippleOnHit to spawned beams if selected
                if (instruction.HasSecondary(MechanicKind.RippleOnHit))
                {
                    spawner.AddModifierSpec("RippleOnHit", ("debugLogs", p.debugLogs || debugLogs));
                }
                // Do not create a one-off beam now; spawner will handle periodic creation
                return;
            }

            // Single-shot beam
            var go = new GameObject("Beam");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            AddMechanicByName(go, "Beam", beamSettings.ToArray());
            InitializeMechanics(go, owner, target);
            subItems.Add(go);
        }

        public void AddModifierToAll(List<GameObject> subItems, MechanicKind kind, ItemParams p)
        {
            if (subItems == null)
                return;
            foreach (var go in subItems)
            {
                if (go == null)
                    continue;
                switch (kind)
                {
                    case MechanicKind.RippleOnHit:
                        // Attach RippleOnHit to supported primaries; skip on incompatible like Orbit-only constructs
                        // It's compatible with Projectile, Beam, Strike, Aura, Ripple (spawns independent rings on hit), but skip Whip for now.
                        if (go.GetComponent<Mechanics.Neuteral.WhipMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'RippleOnHit' on Whip.");
                            break;
                        }
                        AddMechanicByName(
                            go,
                            "RippleOnHit",
                            new (string key, object val)[]
                            {
                                ("debugLogs", p.debugLogs || debugLogs),
                            }
                        );
                        break;
                    case MechanicKind.Orbit:
                        // Skip Orbit if the target is a Beam (incompatible)
                        if (go.GetComponent<Mechanics.Neuteral.BeamMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Orbit' on Beam.");
                            break;
                        }
                        // Skip Orbit on Whip
                        if (go.GetComponent<Mechanics.Neuteral.WhipMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Orbit' on Whip.");
                            break;
                        }
                        // Skip Orbit on Ripple
                        if (go.GetComponent<Mechanics.Neuteral.RippleMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Orbit' on Ripple.");
                            break;
                        }
                        // Skip Orbit on Strike (incompatible by design)
                        if (go.GetComponent<Mechanics.Neuteral.StrikeMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Orbit' on Strike.");
                            break;
                        }
                        // Ensure projectile (if present) does not self-drive when Orbit is added later
                        SetExistingMechanicSetting(go, "Projectile", "disableSelfSpeed", true);
                        AddMechanicByName(
                            go,
                            "Orbit",
                            new (string key, object val)[]
                            {
                                ("radius", p.orbitRadius),
                                ("angularSpeedDeg", p.orbitSpeedDeg > 0 ? p.orbitSpeedDeg : 90f),
                            }
                        );
                        // If Orbit JSON contains a DestroyOnHit override, apply it to existing ProjectileMechanic
                        var orbitJson = LoadAndMergeJsonSettings("Orbit");
                        if (orbitJson.TryGetValue("destroyOnHit", out var odh))
                        {
                            bool val = false;
                            if (odh is bool bt)
                                val = bt;
                            else if (odh is string s && bool.TryParse(s, out var pb))
                                val = pb;
                            SetExistingMechanicSetting(go, "Projectile", "destroyOnHit", val);
                        }
                        break;
                    case MechanicKind.Explosion:
                        AddMechanicByName(
                            go,
                            "Explosion",
                            new (string key, object val)[]
                            {
                                ("debugLogs", p.debugLogs || debugLogs),
                            }
                        );
                        break;
                    case MechanicKind.Bounce:
                        // Incompatible with Aura, Strike, and Whip primaries
                        if (go.GetComponent<Mechanics.Neuteral.AuraMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Bounce' on Aura.");
                            break;
                        }
                        if (go.GetComponent<Mechanics.Neuteral.StrikeMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Bounce' on Strike.");
                            break;
                        }
                        if (go.GetComponent<Mechanics.Neuteral.WhipMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Bounce' on Whip.");
                            break;
                        }
                        // Also incompatible on Ripple
                        if (go.GetComponent<Mechanics.Neuteral.RippleMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Bounce' on Ripple.");
                            break;
                        }
                        AddMechanicByName(go, "Bounce", new (string key, object val)[] { });
                        break;
                    case MechanicKind.Drain:
                        AddMechanicByName(
                            go,
                            "Drain",
                            new (string key, object val)[]
                            {
                                ("radius", p.drainRadius),
                                ("interval", Mathf.Max(0.01f, p.drainInterval)),
                                ("damagePerInterval", Mathf.Max(0, p.drainDamage)),
                                ("lifeStealRatio", Mathf.Clamp01(p.lifeStealRatio)),
                            }
                        );
                        break;
                    case MechanicKind.Lock:
                        // No overrides; rely on JSON defaults (e.g., "Stun Time")
                        AddMechanicByName(go, "Lock", new (string key, object val)[] { });
                        break;
                    case MechanicKind.Track:
                        // Skip Track on Strike (incompatible by design)
                        if (go.GetComponent<Mechanics.Neuteral.StrikeMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Track' on Strike.");
                            break;
                        }
                        // Skip Track on Ripple
                        if (go.GetComponent<Mechanics.Neuteral.RippleMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'Track' on Ripple.");
                            break;
                        }
                        // No overrides; rely on JSON defaults on other primaries
                        AddMechanicByName(go, "Track", new (string key, object val)[] { });
                        break;
                    case MechanicKind.DamageOverTime:
                        // Skip DoT on Whip for now
                        if (go.GetComponent<Mechanics.Neuteral.WhipMechanic>() != null)
                        {
                            Log($"Skipping incompatible modifier 'DamageOverTime' on Whip.");
                            break;
                        }
                        // Ensure projectile (if present) does not self-drive when DamageOverTime is added later
                        SetExistingMechanicSetting(go, "Projectile", "disableSelfSpeed", true);
                        AddMechanicByName(
                            go,
                            "DamageOverTime",
                            new (string key, object val)[]
                            {
                                ("interval", p.drainInterval),
                                ("damagePerInterval", p.drainDamage),
                            }
                        );
                        break;
                }
                InitializeMechanics(go, owner, target);
            }
        }

        private void BuildStrike(
            GameObject root,
            ItemInstruction instruction,
            ItemParams p,
            List<GameObject> subItems
        )
        {
            var go = new GameObject("Strike");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;

            var strikeJson = LoadAndMergeJsonSettings("Strike");
            Color vizColor = Color.black;
            if (strikeJson.TryGetValue("spriteColor", out var sc))
            {
                Color c;
                if (TryParseColor(sc, out c))
                    vizColor = c;
            }

            var settings = new List<(string key, object val)>();
            if (strikeJson.TryGetValue("interval", out var iv))
                settings.Add(("interval", iv));
            if (strikeJson.TryGetValue("damagePerInterval", out var dmg))
                settings.Add(("damagePerInterval", dmg));
            if (strikeJson.TryGetValue("damage", out var dmg2))
                settings.Add(("damage", dmg2));
            settings.Add(("requireMobTag", true));
            settings.Add(("excludeOwner", true));
            settings.Add(("showVisualization", true));
            settings.Add(("vizColor", vizColor));
            settings.Add(("debugLogs", p.debugLogs || debugLogs));

            AddMechanicByName(go, "Strike", settings.ToArray());
            InitializeMechanics(go, owner, target);
            subItems.Add(go);
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
            var primaryJson =
                primaryMechanicListJson != null
                    ? primaryMechanicListJson
                    : Resources.Load<TextAsset>("Primary Mechanic List");
            var modifierJson =
                modifierMechanicListJson != null
                    ? modifierMechanicListJson
                    : Resources.Load<TextAsset>("Modifier Mechanic List");
            var catalog = MechanicCatalog.Load(primaryJson, modifierJson);
            if (catalog == null)
            {
                Log($"MechanicCatalog not available when adding '{mechanicName}'.");
                return null;
            }
            if (!catalog.TryGetPath(mechanicName, out var path) || string.IsNullOrWhiteSpace(path))
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
            var merged = LoadAndMergeJsonSettings(mechanicName);
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
            var primaryJson =
                primaryMechanicListJson != null
                    ? primaryMechanicListJson
                    : Resources.Load<TextAsset>("Primary Mechanic List");
            var modifierJson =
                modifierMechanicListJson != null
                    ? modifierMechanicListJson
                    : Resources.Load<TextAsset>("Modifier Mechanic List");
            var catalog = MechanicCatalog.Load(primaryJson, modifierJson);
            if (catalog == null)
                return false;
            if (!catalog.TryGetPath(mechanicName, out var path) || string.IsNullOrWhiteSpace(path))
                return false;
            var type = MechanicReflection.ResolveTypeFromMechanicPath(path);
            if (type == null)
                return false;
            var comp = go.GetComponent(type) as Component;
            if (comp == null)
                return false;
            return MechanicReflection.ApplyMember(comp, key, val);
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

        private HashSet<MechanicKind> GetModifiersToApply(ItemInstruction instruction)
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
            var primaryJson =
                primaryMechanicListJson != null
                    ? primaryMechanicListJson
                    : Resources.Load<TextAsset>("Primary Mechanic List");
            var list = new List<string>();
            string json = primaryJson != null ? primaryJson.text : null;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(mechanicName))
                return list;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    return list;
                int colon = json.IndexOf(':', nameKey);
                if (colon < 0)
                    return list;
                int q1 = json.IndexOf('"', colon + 1);
                if (q1 < 0)
                    return list;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0)
                    return list;
                string found = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                if (!string.Equals(found, mechanicName, comp))
                    continue;
                int arrKey = json.IndexOf("\"" + arrayName + "\"", searchPos, comp);
                if (arrKey < 0)
                    return list;
                int arrColon = json.IndexOf(':', arrKey);
                if (arrColon < 0)
                    return list;
                int open = json.IndexOf('[', arrColon);
                if (open < 0)
                    return list;
                int depth = 0;
                int i = open;
                for (; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '[')
                        depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                            break;
                    }
                }
                if (i >= json.Length)
                    return list;
                string inner = json.Substring(open + 1, i - open - 1);
                // Extract quoted strings
                int p = 0;
                while (true)
                {
                    int s1 = inner.IndexOf('"', p);
                    if (s1 < 0)
                        break;
                    int s2 = inner.IndexOf('"', s1 + 1);
                    if (s2 < 0)
                        break;
                    string val = inner.Substring(s1 + 1, s2 - s1 - 1);
                    if (!string.IsNullOrWhiteSpace(val))
                        list.Add(val);
                    p = s2 + 1;
                }
                return list;
            }
        }

        private void EnsureOwnerDrain(Transform drainOwner, ItemParams p)
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

        private class _AutoDestroyAfterSeconds : MonoBehaviour
        {
            public float seconds = 5f;
            private float _t;

            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= seconds)
                    Destroy(gameObject);
            }
        }

        // --- JSON settings helpers ---
        private Dictionary<string, object> LoadAndMergeJsonSettings(string mechanicName)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            // Properties
            foreach (var kv in LoadKvpArrayForMechanic(mechanicName, "Properties"))
                dict[kv.Key] = kv.Value;
            // Overrides (primary) and MechanicOverrides (modifier) win over Properties
            foreach (var kv in LoadKvpArrayForMechanic(mechanicName, "Overrides"))
                dict[kv.Key] = kv.Value;
            foreach (var kv in LoadKvpArrayForMechanic(mechanicName, "MechanicOverrides"))
                dict[kv.Key] = kv.Value;
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

        private Dictionary<string, object> LoadKvpArrayForMechanic(
            string mechanicName,
            string arrayName
        )
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            // Check primary then modifier JSONs
            var primaryJson =
                primaryMechanicListJson != null
                    ? primaryMechanicListJson
                    : Resources.Load<TextAsset>("Primary Mechanic List");
            var modifierJson =
                modifierMechanicListJson != null
                    ? modifierMechanicListJson
                    : Resources.Load<TextAsset>("Modifier Mechanic List");

            bool filled = TryExtractKvpArray(primaryJson?.text, mechanicName, arrayName, result);
            if (!filled)
                TryExtractKvpArray(modifierJson?.text, mechanicName, arrayName, result);
            return result;
        }

        private static bool TryExtractKvpArray(
            string json,
            string mechanicName,
            string arrayName,
            Dictionary<string, object> output
        )
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(mechanicName))
                return false;
            int searchPos = 0;
            var comp = StringComparison.OrdinalIgnoreCase;
            while (true)
            {
                int nameKey = json.IndexOf("\"MechanicName\"", searchPos, comp);
                if (nameKey < 0)
                    return false;
                int colon = json.IndexOf(':', nameKey);
                if (colon < 0)
                    return false;
                int q1 = json.IndexOf('"', colon + 1);
                if (q1 < 0)
                    return false;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0)
                    return false;
                string found = json.Substring(q1 + 1, q2 - q1 - 1);
                searchPos = q2 + 1;
                if (!string.Equals(found, mechanicName, comp))
                    continue;

                // Found matching entry; find requested array
                int arrKey = json.IndexOf("\"" + arrayName + "\"", searchPos, comp);
                if (arrKey < 0)
                    return false;
                int arrColon = json.IndexOf(':', arrKey);
                if (arrColon < 0)
                    return false;
                int open = json.IndexOf('[', arrColon);
                if (open < 0)
                    return false;
                int depth = 0;
                int i = open;
                for (; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '[')
                        depth++;
                    else if (c == ']')
                    {
                        depth--;
                        if (depth == 0)
                            break;
                    }
                }
                if (i >= json.Length)
                    return false;
                string inner = json.Substring(open + 1, i - open - 1);

                // Parse items: { "key": value }
                int p = 0;
                while (true)
                {
                    int ob = inner.IndexOf('{', p);
                    if (ob < 0)
                        break;
                    int cb = inner.IndexOf('}', ob + 1);
                    if (cb < 0)
                        break;
                    string obj = inner.Substring(ob + 1, cb - ob - 1);
                    int kq1 = obj.IndexOf('"');
                    if (kq1 >= 0)
                    {
                        int kq2 = obj.IndexOf('"', kq1 + 1);
                        if (kq2 > kq1)
                        {
                            string key = obj.Substring(kq1 + 1, kq2 - kq1 - 1);
                            int vcolon = obj.IndexOf(':', kq2 + 1);
                            if (vcolon > 0)
                            {
                                string raw = obj.Substring(vcolon + 1).Trim();
                                object val = ParsePrimitive(raw);
                                if (!string.IsNullOrWhiteSpace(key))
                                    output[key] = val;
                            }
                        }
                    }
                    p = cb + 1;
                }
                return output.Count > 0;
            }
        }

        private static object ParsePrimitive(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;
            // Strip quotes if string
            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            {
                return raw.Substring(1, raw.Length - 2);
            }
            if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            if (raw.Contains("."))
            {
                if (float.TryParse(raw, out var f))
                    return f;
            }
            else
            {
                if (int.TryParse(raw, out var i))
                    return i;
            }
            return raw; // fallback as string
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
