using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Shoots a forward beam that expands lengthwise (tombstone shape: flat base + half-circle head),
    /// dealing continuous damage at intervals to targets within the beam. Destruction is now governed only by lifetime (if > 0) rather than max distance.
    public class BeamMechanic : MonoBehaviour, IMechanic
    {
        [Header("Beam Shape & Motion")]
        [Tooltip("World width of the beam (does not expand).")]
        [Min(0f)]
        public float beamWidth = 1f;

        [Tooltip("Units per second the beam head travels (replaces extendSpeed).")]
        [Min(0f)]
        public float speed = 20f;

        // Backward compatibility: old JSON might still supply extendSpeed. If >0 and speed left default, adopt it.
        [HideInInspector]
        public float extendSpeed = 0f; // deprecated alias

        [Tooltip(
            "Direction string: right,left,up,down or angle degrees (e.g., 45). Defaults to right."
        )]
        public string direction = "right";

        [Header("Damage & Timing")]
        [Tooltip(
            "Seconds between damage ticks (separate from spawner interval). If 0, defaults to 0.1."
        )]
        [Min(0f)]
        public float damageInterval = 0.1f; // new explicit damage interval

        // Backward compatibility: if older JSON still sets 'interval' expecting damage cadence, capture it here.
        [HideInInspector]
        public float interval = 0f; // deprecated alias; will migrate into damageInterval at Initialize if > 0

        [Min(0)]
        public int damagePerInterval = 2;

        [Tooltip("Only damage colliders tagged 'Mob' in their parent chain.")]
        public bool requireMobTag = true;

        [Tooltip("Skip owner and its hierarchy.")]
        public bool excludeOwner = true;

        [Header("Targeting & Viz")]
        public LayerMask targetLayers = ~0;
        public bool showVisualization = true;
        public Color vizColor = new Color(1f, 1f, 1f, 0.5f);
        public int vizSortingOrder = 0;
        public bool debugLogs = false;

        [Header("Redirect Behavior")]
        [Tooltip(
            "If true, when Redirect(...) is called, the current head position is preserved (tail/base shifts). If false, beam collapses & regrows from base when not anchoring."
        )]
        public bool preserveHeadOnRedirect = true; // formerly preserveTipOnRedirect / preserveTipOnBounce

        [Header("Anchored Tail (Experimental)")]
        [Tooltip(
            "If true, beam tail visually connects back to owner (player) via polyline; only final segment damages."
        )]
        public bool anchorTailToPlayer = true;

        [Tooltip(
            "When anchoring is enabled, each redirect adds a corner node forming a zig-zag path."
        )]
        public bool segmentOnRedirect = true; // formerly segmentOnBounce

        [Header("Segmentation (Reusable)")]
        [Tooltip(
            "If true, treat beam as composed of named segments listed in 'segments'. Enables segment-targeted modifiers."
        )]
        public bool isSegmented = true;

        [Tooltip("Ordered list of segment identifiers. Convention: 'head','tail'.")]
        public List<string> segments = new List<string> { "head", "tail" };

        [Tooltip(
            "Which segment name should bounce use as trigger (e.g., 'head','tail','any'). Empty = legacy."
        )]
        public string bounceSegment = "head";

        [Tooltip(
            "Space/comma separated segment names whose damage should be individually reported to Drain. Empty = total only."
        )]
        public string drainSegments = "";
        private int _headDamageThisTick;
        private int _tailDamageThisTick;
        private List<string> _cachedDrainSegments;

        [Header("Lifetime")]
        [Tooltip(
            "Maximum lifetime in seconds before the beam despawns regardless of distance (0 = unlimited)."
        )]
        public float lifetime = 0f;

        private float _lifeTimer;

        private MechanicContext _ctx;
        private float _timer;
        private float _length; // current length achieved (no cap – grows until lifetime ends or external destroy)
        private Vector2 _dir = Vector2.right; // normalized
        private ContactFilter2D _filter;
        private Collider2D[] _hits = new Collider2D[64];

        // Hit shape
        private Transform _hitRoot;
        private BoxCollider2D _box;
        private CircleCollider2D _head; // circle collider for beam head (formerly tip)

        // Viz
        private Transform _vizRoot;
        private SpriteRenderer _rectSr;
        private SpriteRenderer _headSr;
        private Sprite _circleSprite;
        private Sprite _squareSprite;

        // Dynamic targeting / tracking support
        private bool _dynamicTracking; // true if a TrackMechanic is attached
        private float _retargetTimer; // timer for periodic retargeting
        private float _retargetInterval = 0.1f; // default retarget cadence
        private Transform _currentTarget; // cached target transform
        private float _turnRateDegPerSec = 720f; // default turn speed when tracking

        // Anchored tail representation
        private readonly List<Vector3> _pathNodes = new(); // path nodes: owner base + corners
        private LineRenderer _tailLine; // legacy (unused when segmented mode active)
        private Transform _tailParent; // parent object holding segment sprites+colliders

        private class TailSegment
        {
            public GameObject go;
            public Transform transform;
            public Transform body; // child that visually/scales forward
            public SpriteRenderer sr;
            public BoxCollider2D collider;
            public Vector3 start;
            public Vector3 end;
            public bool finalized;

            // Snapshot data
            public float createdTime;
            public float finalizedTime;
            public Vector3 startWorldPosition;
            public Vector3 endWorldPosition;
            public Vector3 bodyLocalScaleAtFinalize;
            public Quaternion rotationAtFinalize;
        }

        private readonly List<TailSegment> _tailSegments = new();
        private TailSegment _activeSegment; // current growing segment
        private readonly List<SegmentSnapshot> _segmentSnapshots = new();
        private float _globalTime; // monotonic time since Initialize for snapshot timestamps
        private Vector3 _spawnedRootWorld; // world position of beam root at spawn (Beam_Spawned / this transform initial)
        private Quaternion _spawnedRootRotation; // initial rotation (may not be needed but stored for completeness)
        private bool _headHitThisTick; // whether head collider produced at least one unique hit this damage tick (anchored mode bounce gating)

        public struct SegmentSnapshot
        {
            public int index;
            public float createdTime;
            public float finalizedTime;
            public Vector3 start;
            public Vector3 end;
            public Vector3 bodyLocalScale;
            public Quaternion rotation;
        }

        /// <summary>Read-only copy of current segment snapshot records.</summary>
        public IReadOnlyList<SegmentSnapshot> SegmentSnapshots => _segmentSnapshots;
        private bool SegmentedTail => UsingAnchoredTail; // for now segmented replaces line completely when anchoring
        private bool UsingAnchoredTail => anchorTailToPlayer;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            _length = 0f;
            _lifeTimer = 0f;
            _globalTime = 0f;
            _spawnedRootWorld = transform.position;
            _spawnedRootRotation = transform.rotation;
            // Determine if we should auto-aim / track
            var tracker = GetComponent<Mechanics.Order.TrackMechanic>();
            _dynamicTracking = tracker != null; // presence of TrackMechanic enables dynamic updates
            if (tracker != null)
            {
                // Borrow its public config if available
                _turnRateDegPerSec = Mathf.Max(0f, tracker.turnRateDegPerSec);
                _retargetInterval = Mathf.Max(0.01f, tracker.retargetInterval);
                // If TrackMechanic has a searchRadius we could honor it later (not required now)
            }

            // Initial aim: prefer explicit context target, else nearest mob, else parsed direction
            _currentTarget = (_ctx != null ? _ctx.Target : null);
            if (_currentTarget == null)
            {
                _currentTarget = FindNearestMob();
            }
            if (_currentTarget != null)
            {
                Vector2 to = (Vector2)(_currentTarget.position - transform.position);
                if (to.sqrMagnitude > 1e-6f)
                    _dir = to.normalized;
                else
                    _dir = Vector2.right;
            }
            else
            {
                _dir = ParseDirection(direction);
            }

            // Create hit root rotated to face direction; local +Y is forward
            _hitRoot = (new GameObject("BeamHit")).transform;
            _hitRoot.SetParent(transform, false);
            _hitRoot.localPosition = Vector3.zero;
            _hitRoot.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(_dir.x, _dir.y, 0f)
            );

            if (!UsingAnchoredTail)
            {
                _box = _hitRoot.gameObject.AddComponent<BoxCollider2D>();
                _box.isTrigger = true;
                // Legacy (non-anchored) head collider retained
                _head = _hitRoot.gameObject.AddComponent<CircleCollider2D>();
                _head.isTrigger = true;
            }
            else
            {
                // Ensure no stray circle collider on root (Beam_Spawned) or hit root when anchored mode (head collider not needed)
                var strayRoot = GetComponent<CircleCollider2D>();
                if (strayRoot != null)
                    Destroy(strayRoot);
                var strayHit = _hitRoot.GetComponent<CircleCollider2D>();
                if (strayHit != null)
                    Destroy(strayHit);
            }

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };

            if (showVisualization)
                EnsureVisualization(); // will also create head sprite; in anchored mode we'll add head collider there if needed

            if (UsingAnchoredTail)
            {
                // Anchored mode: active tail segment does primary sweeping damage; head circle collider (added in visualization) is used to gate bounce logic.
                InitializeAnchoredTail(); // set up tail segments
            }

            UpdateGeometry();
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;
            _globalTime += dt;

            // Re-lock finalized segments to their stored world transforms (in case parenting changes moved them)
            if (UsingAnchoredTail && _tailSegments.Count > 0)
            {
                foreach (var seg in _tailSegments)
                {
                    if (seg == null || !seg.finalized)
                        continue;
                    // Use stored world data
                    if (seg.transform != null)
                    {
                        seg.transform.position = seg.startWorldPosition; // parent pivot at start
                        seg.transform.rotation = seg.rotationAtFinalize;
                    }
                    if (seg.body != null && seg.bodyLocalScaleAtFinalize != Vector3.zero)
                    {
                        seg.body.localScale = seg.bodyLocalScaleAtFinalize;
                        // Ensure body localPosition is half-length along +Y (since we stored scale not position)
                        float len = seg.bodyLocalScaleAtFinalize.y;
                        seg.body.localPosition = new Vector3(0f, len * 0.5f, 0f);
                    }
                }
            }

            // Static anchored tail: base position is fixed at spawn (no per-frame base update).

            // Dynamic tracking: periodically reacquire and rotate toward target
            if (_dynamicTracking)
            {
                _retargetTimer += dt;
                if (_currentTarget == null || _retargetTimer >= _retargetInterval)
                {
                    _currentTarget = _ctx.Target != null ? _ctx.Target : FindNearestMob();
                    _retargetTimer = 0f;
                }
                if (_currentTarget != null)
                {
                    Vector2 to = (Vector2)(_currentTarget.position - transform.position);
                    if (to.sqrMagnitude > 1e-6f)
                    {
                        Vector2 desired = to.normalized;
                        _dir = RotateToward(_dir, desired, _turnRateDegPerSec * dt);
                        if (_hitRoot != null)
                        {
                            _hitRoot.localRotation = Quaternion.FromToRotation(
                                Vector3.up,
                                new Vector3(_dir.x, _dir.y, 0f)
                            );
                        }
                    }
                }
            }

            // Extend head forward using unified speed (legacy extendSpeed migrates if set)
            if (speed <= 0f && extendSpeed > 0f)
                speed = extendSpeed; // one-time migration (extendSpeed persists only for initial assignment)
            _length = Mathf.Max(0f, _length + speed * dt);
            UpdateGeometry();

            // Damage tick (moved before lifetime destruction to guarantee at least one tick occurs if lifetime ~= interval)
            // Back-compat migration: if legacy 'interval' was set via JSON and damageInterval left default (or zero), adopt it once.
            if (damageInterval <= 0f && interval > 0f)
                damageInterval = interval;
            float tickEvery = Mathf.Max(0.01f, damageInterval);
            _timer += dt;
            if (_timer >= tickEvery)
            {
                _timer = 0f;
                int totalDamage = DoDamageTick();
                // If we hit anything, optionally trigger an Explosion once per tick at the first hit position
                if (totalDamage > 0)
                {
                    var explode = GetComponent<Mechanics.Chaos.ExplosionMechanic>();
                    if (explode != null)
                    {
                        // Find an epicenter near the tip for this tick
                        Vector2 epicenter = (Vector2)transform.position + _dir * _length;
                        explode.TriggerExplosion(epicenter);
                    }

                    // Ripple-on-hit modifier: trigger a ripple chain from the beam tip direction
                    var rippleOnHit = GetComponent<Mechanics.Chaos.RippleOnHitMechanic>();
                    if (rippleOnHit != null)
                    {
                        // We don't have a specific collider reference here; pass null target
                        Vector2 epicenter = (Vector2)transform.position + _dir * _length;
                        rippleOnHit.TriggerFrom(null, epicenter);
                    }
                }
                // Segmented / Drain reporting
                if (totalDamage > 0 && _ctx.Owner != null)
                {
                    var drain =
                        _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                    if (drain != null)
                    {
                        if (isSegmented && !string.IsNullOrWhiteSpace(drainSegments))
                        {
                            var wanted = ParseDrainSegments();
                            if (wanted.Count == 0)
                            {
                                drain.ReportDamage(totalDamage);
                            }
                            else
                            {
                                foreach (var seg in wanted)
                                {
                                    int segDmg = 0;
                                    if (seg == "head")
                                        segDmg = _headDamageThisTick;
                                    else if (seg == "tail")
                                        segDmg = _tailDamageThisTick;
                                    else if (seg == "any")
                                        segDmg = totalDamage;
                                    if (segDmg > 0)
                                        drain.ReportDamage(segDmg);
                                }
                            }
                        }
                        else
                        {
                            drain.ReportDamage(totalDamage);
                        }
                    }
                }
                // Bounce integration via segmentation rule
                if (ShouldBounce(totalDamage))
                {
                    if (HandleBounceOnHit())
                        return; // beam destroyed by bounce
                }
            }

            // No distance-based destruction anymore.

            // Lifetime countdown (after damage tick to avoid zero-tick beams when lifetime <= interval)
            if (lifetime > 0f)
            {
                _lifeTimer += dt;
                if (_lifeTimer >= lifetime)
                {
                    if (debugLogs)
                        Debug.Log("[BeamMechanic] Lifetime expired -> destroy", this);
                    Destroy(gameObject);
                    return;
                }
                else if (debugLogs && _lifeTimer < dt * 1.5f) // first frame diagnostic
                {
                    if (lifetime <= damageInterval)
                    {
                        Debug.Log(
                            $"[BeamMechanic] Diagnostic: lifetime ({lifetime}) <= damageInterval ({damageInterval}) may reduce tick count.",
                            this
                        );
                    }
                }
            }

            if (UsingAnchoredTail)
            {
                UpdateAnchoredTailVisualization();
            }
        }

        private int DoDamageTick()
        {
            int total = 0;
            _headHitThisTick = false;
            _headDamageThisTick = 0;
            _tailDamageThisTick = 0;
            if (UsingAnchoredTail)
            {
                // Active segment + (optional) head collider; dedupe hits across both.
                var seen = new HashSet<Collider2D>();
                if (
                    _activeSegment != null
                    && _activeSegment.collider != null
                    && !_activeSegment.finalized
                )
                {
                    int count = _activeSegment.collider.Overlap(_filter, _hits);
                    if (debugLogs)
                    {
                        var b = _activeSegment.collider.bounds;
                        Debug.Log(
                            $"[BeamMechanic] Tick overlap activeSeg count={count} boundsCenter={b.center} size={b.size} dir={_dir} length={_length}",
                            this
                        );
                    }
                    int before = total;
                    total += DamageUniqueHitsVerbose(count, seen, _activeSegment.collider);
                    if (isSegmented)
                        _tailDamageThisTick += (total - before);
                    if (debugLogs && total == before && count == 0)
                        DebugNoHitFrame();
                }
                if (_head != null)
                {
                    int hc = _head.Overlap(_filter, _hits);
                    int beforeHead = total;
                    total += DamageUniqueHitsVerbose(hc, seen, _head);
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[BeamMechanic] Head overlap rawCount={hc} uniqueAdded={(total - beforeHead)}",
                            this
                        );
                    }
                    if (total > beforeHead && hc > 0)
                    {
                        _headHitThisTick = true;
                        if (isSegmented)
                            _headDamageThisTick += (total - beforeHead);
                        if (debugLogs)
                            Debug.Log(
                                "[BeamMechanic] Head collider registered hit(s) this tick",
                                this
                            );
                    }
                }
                return total;
            }
            if (_box == null || _head == null)
                return 0;
            // Legacy single-body path
            int c = _box.Overlap(_filter, _hits);
            int beforeBox = total;
            total += DamageHits(c);
            if (isSegmented)
                _tailDamageThisTick += (total - beforeBox);
            c = _head.Overlap(_filter, _hits);
            int beforeHead2 = total;
            total += DamageHits(c);
            if (isSegmented)
                _headDamageThisTick += (total - beforeHead2);
            return total;
        }

        private bool ShouldBounce(int totalDamage)
        {
            if (!isSegmented)
            {
                return (UsingAnchoredTail && _headHitThisTick)
                    || (!UsingAnchoredTail && totalDamage > 0);
            }
            if (string.IsNullOrWhiteSpace(bounceSegment))
                return (UsingAnchoredTail && _headHitThisTick)
                    || (!UsingAnchoredTail && totalDamage > 0);
            switch (bounceSegment)
            {
                case "head":
                    return _headDamageThisTick > 0;
                case "tail":
                    return _tailDamageThisTick > 0;
                case "any":
                    return totalDamage > 0;
                default:
                    return totalDamage > 0;
            }
        }

        private List<string> ParseDrainSegments()
        {
            if (_cachedDrainSegments != null)
                return _cachedDrainSegments;
            _cachedDrainSegments = new List<string>();
            if (string.IsNullOrWhiteSpace(drainSegments))
                return _cachedDrainSegments;
            var parts = drainSegments.Split(
                new char[] { ' ', ',', ';' },
                System.StringSplitOptions.RemoveEmptyEntries
            );
            foreach (var p in parts)
            {
                if (!_cachedDrainSegments.Contains(p))
                    _cachedDrainSegments.Add(p);
            }
            return _cachedDrainSegments;
        }

        private int DamageUniqueHits(int count, HashSet<Collider2D> seen)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c == null)
                    continue;
                if (!seen.Add(c))
                    continue; // already processed
                if (excludeOwner && IsOwnerRelated(c))
                    continue;
                if (requireMobTag && !HasMobTagInParents(c.transform))
                    continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;
                dmg.TakeDamage(damagePerInterval, _dir, Vector2.zero);
                total += damagePerInterval;
                if (debugLogs)
                    Debug.Log($"[BeamMechanic] Damaged {c.name} for {damagePerInterval}", this);
                var locker = GetComponent<Mechanics.Order.LockMechanic>();
                if (locker != null)
                    locker.TryApplyTo(c.transform);
                var dot = GetComponent<Mechanics.Corruption.DamageOverTimeMechanic>();
                if (dot != null)
                    dot.TryApplyTo(c.transform);
            }
            return total;
        }

        // Verbose variant for anchored mode; logs decision path for each skipped collider
        private int DamageUniqueHitsVerbose(int count, HashSet<Collider2D> seen, Collider2D source)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c == null)
                {
                    if (debugLogs)
                        Debug.Log("[BeamMechanic] Skip null collider index=" + i, this);
                    continue;
                }
                if (!seen.Add(c))
                {
                    if (debugLogs)
                        Debug.Log($"[BeamMechanic] Skip duplicate {c.name}", this);
                    continue;
                }
                if (excludeOwner && IsOwnerRelated(c))
                {
                    if (debugLogs)
                        Debug.Log($"[BeamMechanic] Skip owner-related {c.name}", this);
                    continue;
                }
                if (requireMobTag && !HasMobTagInParents(c.transform))
                {
                    if (debugLogs)
                        Debug.Log($"[BeamMechanic] Skip no Mob tag {c.name}", this);
                    continue;
                }
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null)
                {
                    if (debugLogs)
                        Debug.Log($"[BeamMechanic] Skip no IDamageable {c.name}", this);
                    continue;
                }
                if (!dmg.IsAlive)
                {
                    if (debugLogs)
                        Debug.Log($"[BeamMechanic] Skip dead {c.name}", this);
                    continue;
                }
                dmg.TakeDamage(damagePerInterval, _dir, Vector2.zero);
                total += damagePerInterval;
                if (debugLogs)
                {
                    var b = c.bounds;
                    Debug.Log(
                        $"[BeamMechanic] HIT {c.name} dmg={damagePerInterval} center={b.center} size={b.size} via={source.name}",
                        this
                    );
                }
                var locker = GetComponent<Mechanics.Order.LockMechanic>();
                if (locker != null)
                    locker.TryApplyTo(c.transform);
                var dot = GetComponent<Mechanics.Corruption.DamageOverTimeMechanic>();
                if (dot != null)
                    dot.TryApplyTo(c.transform);
            }
            return total;
        }

        private int DamageHits(int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c == null)
                    continue;
                if (excludeOwner && IsOwnerRelated(c))
                    continue;
                if (requireMobTag && !HasMobTagInParents(c.transform))
                    continue;

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;

                Vector2 hp = c.bounds.ClosestPoint(transform.position);
                dmg.TakeDamage(damagePerInterval, _dir, Vector2.zero);
                total += damagePerInterval;
                if (debugLogs)
                    Debug.Log($"[BeamMechanic] Damaged {c.name} for {damagePerInterval}", this);

                // Apply Lock modifier if present on this payload
                var locker = GetComponent<Mechanics.Order.LockMechanic>();
                if (locker != null)
                {
                    locker.TryApplyTo(c.transform);
                }
                // Apply DoT modifier if present on this payload
                var dot = GetComponent<Mechanics.Corruption.DamageOverTimeMechanic>();
                if (dot != null)
                {
                    dot.TryApplyTo(c.transform);
                }
            }
            return total;
        }

        private void UpdateGeometry()
        {
            float w = Mathf.Max(0.0001f, beamWidth);
            float r = w * 0.5f; // tip circle radius and half-width
            // Treat _length as the distance from base to head center; body extends up to (length - r)
            float bodyLen = Mathf.Max(0f, _length - r);
            if (!UsingAnchoredTail && _box != null)
            {
                _box.enabled = true;
                _box.size = new Vector2(w, Mathf.Max(0.0001f, bodyLen));
                _box.offset = new Vector2(0f, bodyLen * 0.5f);
            }
            if (_head != null)
            {
                _head.radius = r;
                if (UsingAnchoredTail)
                {
                    // Head collider centered; visualization positions head sprite appropriately so we keep zero offset.
                    _head.offset = Vector2.zero;
                }
                else
                {
                    _head.offset = new Vector2(0f, bodyLen);
                }
            }

            if (showVisualization)
                UpdateVisualization(w, bodyLen, r);
        }

        private void EnsureVisualization()
        {
            if (_vizRoot != null)
                return;
            // Root that inherits the hit orientation (so +Y is beam forward)
            _vizRoot = new GameObject("BeamViz").transform;
            _vizRoot.SetParent(_hitRoot != null ? _hitRoot : transform, false);
            _vizRoot.localPosition = Vector3.zero;
            _vizRoot.localRotation = Quaternion.identity;

            // Separate children so each sprite has its own transform (skip rect if using anchored tail visual)
            GameObject rectGo = null;
            if (!UsingAnchoredTail)
            {
                rectGo = new GameObject("Rect");
                rectGo.transform.SetParent(_vizRoot, false);
            }
            var tipGo = new GameObject("Head");
            tipGo.transform.SetParent(_vizRoot, false);

            if (rectGo != null)
                _rectSr = rectGo.AddComponent<SpriteRenderer>();
            _headSr = tipGo.AddComponent<SpriteRenderer>();

            if (_rectSr != null)
            {
                _rectSr.sortingOrder = vizSortingOrder;
                _rectSr.color = vizColor;
            }
            if (_headSr != null)
            {
                _headSr.sortingOrder = vizSortingOrder;
                _headSr.color = vizColor;
            }

            // Prefer cached generator sprites to avoid allocating textures per beam
            if (_squareSprite == null)
            {
                var sq = Game.Procederal.ProcederalItemGenerator.GetUnitSquareSprite();
                _squareSprite = sq != null ? sq : GenerateUnitSquare();
            }
            if (_circleSprite == null)
            {
                var ci = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                _circleSprite = ci != null ? ci : GenerateUnitCircle(64);
            }

            if (_rectSr != null)
                _rectSr.sprite = _squareSprite;
            if (_headSr != null)
                _headSr.sprite = _circleSprite;
            // Anchored tail: ensure a head collider exists (used for bounce gating + optional damage overlap)
            if (UsingAnchoredTail && _headSr != null)
            {
                var hc = _headSr.GetComponent<CircleCollider2D>();
                if (hc == null)
                {
                    hc = _headSr.gameObject.AddComponent<CircleCollider2D>();
                    hc.isTrigger = true;
                }
                _head = hc;
            }

            // If anchoring tail, ensure tail parent (if already created earlier) is re-parented under head for hierarchical coupling.
            if (UsingAnchoredTail && _tailParent != null)
            {
                // Ensure tail parent is a child of the head visual (user requirement: tail is child of "head").
                var headTransform = _headSr != null ? _headSr.transform : null;
                if (headTransform != null && _tailParent.parent != headTransform)
                {
                    _tailParent.SetParent(headTransform, true); // keep world positions
                }
            }
        }

        private void UpdateVisualization(float w, float rectLen, float r)
        {
            if (_vizRoot == null)
                EnsureVisualization();
            // Skip rectangle visual entirely when using anchored tail; tail replaces the body depiction.
            if (!UsingAnchoredTail && _rectSr != null)
            {
                _rectSr.enabled = rectLen > 0.0001f;
                _rectSr.color = vizColor;
                _rectSr.sortingOrder = vizSortingOrder;
                _rectSr.transform.localPosition = new Vector3(0f, rectLen * 0.5f, 0f);
                _rectSr.transform.localScale = new Vector3(w, rectLen, 1f);
            }
            else if (UsingAnchoredTail && _rectSr != null && _rectSr.enabled)
            {
                _rectSr.enabled = false; // ensure hidden
            }
            if (_headSr != null)
            {
                _headSr.enabled = r > 0f;
                _headSr.color = vizColor;
                _headSr.sortingOrder = vizSortingOrder;
                // Position head so its center is at bodyLen, visually the flat body plus semicircle
                _headSr.transform.localPosition = new Vector3(0f, rectLen + r, 0f);
                _headSr.transform.localScale = new Vector3(w, w, 1f);
            }
        }

        private Sprite GenerateUnitCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            int cx = size / 2;
            int cy = size / 2;
            float rad = size * 0.5f - 0.5f;
            float r2 = rad * rad;
            var colors = new Color[size * size];
            Color on = Color.white;
            Color off = new Color(1, 1, 1, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - cx + 0.5f);
                    float dy = (y - cy + 0.5f);
                    colors[y * size + x] = (dx * dx + dy * dy) <= r2 ? on : off;
                }
            }
            tex.SetPixels(colors);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private Sprite GenerateUnitSquare()
        {
            int size = 4;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            var colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Vector2 ParseDirection(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return Vector2.right;
            switch (s.Trim().ToLowerInvariant())
            {
                case "right":
                    return Vector2.right;
                case "left":
                    return Vector2.left;
                case "up":
                    return Vector2.up;
                case "down":
                    return Vector2.down;
                default:
                    if (float.TryParse(s, out var deg))
                    {
                        float rad = deg * Mathf.Deg2Rad;
                        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
                    }
                    return Vector2.right;
            }
        }

        private bool HasMobTagInParents(Transform t)
        {
            while (t != null)
            {
                if (t.CompareTag("Mob"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private bool IsOwnerRelated(Collider2D c)
        {
            if (_ctx == null || _ctx.Owner == null || c == null)
                return false;
            var o = _ctx.Owner;
            if (c.transform == o || c.transform.IsChildOf(o) || o.IsChildOf(c.transform))
                return true;
            if (c.attachedRigidbody != null)
            {
                var rt = c.attachedRigidbody.transform;
                if (rt == o || rt.IsChildOf(o) || o.IsChildOf(rt))
                    return true;
            }
            return c.transform.root == o.root;
        }

        // Acquire nearest mob (tag "Mob"). Similar to TrackMechanic logic but localized here.
        private Transform FindNearestMob()
        {
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
                return null;
            Vector3 origin = transform.position;
            float bestDist2 = float.MaxValue;
            Transform best = null;
            foreach (var go in mobs)
            {
                if (go == null)
                    continue;
                float d2 = (go.transform.position - origin).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = go.transform;
                }
            }
            return best;
        }

        // Rotate current direction toward desired by at most maxDeg degrees this step.
        private static Vector2 RotateToward(Vector2 from, Vector2 to, float maxDeg)
        {
            if (from.sqrMagnitude < 1e-6f)
                return to.normalized;
            if (to.sqrMagnitude < 1e-6f)
                return from.normalized;
            float curAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(curAngle, targetAngle);
            float step = Mathf.Clamp(delta, -maxDeg, maxDeg);
            float newAngle = curAngle + step;
            float rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }

        /// <summary>
        /// Redirects the beam in a new direction. Call this from external mechanics (e.g. a bounce or steering system).
        /// Respects preserveHeadOnRedirect and segmentOnRedirect (when anchored tail is enabled).
        /// </summary>
        public void Redirect(Vector2 newDirection)
        {
            if (newDirection.sqrMagnitude < 1e-6f)
                return;
            Vector3 oldDir3 = new Vector3(_dir.x, _dir.y, 0f);
            Vector3 headPos = transform.position + oldDir3 * _length;
            _dir = newDirection.normalized;

            if (UsingAnchoredTail && segmentOnRedirect)
            {
                float r = beamWidth * 0.5f;
                // We want the previous segment to end exactly where the new one begins with no gap.
                // Previous segment's end should coincide with the head's base (not subtracting radius twice).
                // headPos currently = previous root + oldDir * _length (head center). Head base is headPos - oldDir*r.
                Vector3 previousHeadBase = headPos - oldDir3 * r;
                _pathNodes.Add(previousHeadBase);
                // Finalize current active segment (disable collider so only new segment will damage)
                if (_activeSegment != null)
                {
                    _activeSegment.finalized = true;
                    if (_activeSegment.collider != null)
                        _activeSegment.collider.enabled = false;
                    // Capture finalize snapshot
                    _activeSegment.finalizedTime = _globalTime;
                    _activeSegment.endWorldPosition = _activeSegment.end;
                    _activeSegment.bodyLocalScaleAtFinalize =
                        _activeSegment.body != null ? _activeSegment.body.localScale : Vector3.one;
                    _activeSegment.rotationAtFinalize = _activeSegment.transform.rotation;
                    _segmentSnapshots.Add(
                        new SegmentSnapshot
                        {
                            index = _tailSegments.IndexOf(_activeSegment),
                            createdTime = _activeSegment.createdTime,
                            finalizedTime = _activeSegment.finalizedTime,
                            start = _activeSegment.startWorldPosition,
                            end = _activeSegment.endWorldPosition,
                            bodyLocalScale = _activeSegment.bodyLocalScaleAtFinalize,
                            rotation = _activeSegment.rotationAtFinalize,
                        }
                    );
                }
                // Preserve head center position: do NOT move transform. Start a new segment from previous head base.
                // Tail parent remains where it is (already under head). We just spawn a new active segment with start=previousHeadBase.
                float headCenterDistanceFromRoot = _length; // store current head center distance
                if (_tailParent != null)
                {
                    CreateNewActiveSegment(previousHeadBase);
                }
                // Keep _length unchanged so visual head center does not jump.
                // New active segment will grow from its start toward current head base; initialize its start=end so it stretches next frame.
                if (_activeSegment != null)
                {
                    _activeSegment.start = previousHeadBase;
                    _activeSegment.end = previousHeadBase;
                    _activeSegment.startWorldPosition = previousHeadBase;
                    _activeSegment.endWorldPosition = previousHeadBase;
                }
            }
            else if (!UsingAnchoredTail && preserveHeadOnRedirect && _length > 0.0001f)
            {
                Vector3 newBase = headPos - new Vector3(_dir.x, _dir.y, 0f) * _length;
                transform.position = newBase;
                if (debugLogs)
                    Debug.Log("[BeamMechanic] Redirect preserving tip; new base=" + newBase, this);
            }
            else if (!UsingAnchoredTail)
            {
                _length = 0f;
            }

            if (_hitRoot != null)
            {
                _hitRoot.localRotation = Quaternion.FromToRotation(
                    Vector3.up,
                    new Vector3(_dir.x, _dir.y, 0f)
                );
            }
            UpdateGeometry();
        }

        // Anchored tail helpers
        private void OnDestroy()
        {
            // Cleanup tail line if it exists and is not part of another object
            if (_tailLine != null)
            {
                var go = _tailLine.gameObject;
                _tailLine = null;
                if (go != null)
                {
                    Destroy(go);
                }
            }
            if (_tailParent != null)
            {
                Destroy(_tailParent.gameObject);
                _tailSegments.Clear();
                _activeSegment = null;
            }
        }

        private void InitializeAnchoredTail()
        {
            // Segmented tail: each segment is a rectangular sprite stretching between corner nodes and head.
            _pathNodes.Clear();
            Vector3 basePos =
                _ctx != null && _ctx.Owner != null ? _ctx.Owner.position : transform.position;
            _pathNodes.Add(basePos);
            if (_tailParent == null)
            {
                var parentGo = new GameObject("Tail");
                // Parent under head once head exists; temporarily under vizRoot so relative alignment is stable until EnsureVisualization runs.
                parentGo.transform.SetParent(_vizRoot != null ? _vizRoot : transform, false);
                _tailParent = parentGo.transform;
            }
            else
            {
                // Clear any existing children
                for (int i = _tailParent.childCount - 1; i >= 0; i--)
                {
                    Destroy(_tailParent.GetChild(i).gameObject);
                }
                _tailSegments.Clear();
            }
            CreateNewActiveSegment(basePos);
            if (_rectSr != null)
                _rectSr.enabled = false; // hide single body since we replace with segments
            // Disable legacy line renderer if present
            if (_tailLine != null)
            {
                _tailLine.enabled = false;
            }
            // Disable legacy single box collider (segments + head will handle damage)
            if (_box != null)
            {
                _box.enabled = false;
            }
            // Tail will be (or has been) reparented under head in EnsureVisualization.
        }

        private void CreateNewActiveSegment(Vector3 startPos)
        {
            var segGo = new GameObject("Seg" + _tailSegments.Count);
            segGo.transform.SetParent(_tailParent, false);
            var sr = segGo.AddComponent<SpriteRenderer>();
            sr.sprite = _squareSprite != null ? _squareSprite : GenerateUnitSquare();
            sr.color = vizColor;
            sr.sortingOrder = vizSortingOrder; // same order as head
            // Body child that actually scales so parent transform (start point) remains fixed
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(segGo.transform, false);
            var bodySr = bodyGo.AddComponent<SpriteRenderer>();
            bodySr.sprite = sr.sprite;
            bodySr.color = vizColor;
            bodySr.sortingOrder = vizSortingOrder;
            var col = bodyGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            var seg = new TailSegment
            {
                go = segGo,
                transform = segGo.transform,
                body = bodyGo.transform,
                sr = bodySr,
                collider = col,
                start = startPos,
                end = startPos,
                finalized = false,
                createdTime = _globalTime,
                finalizedTime = -1f,
                startWorldPosition = startPos,
                endWorldPosition = startPos,
                bodyLocalScaleAtFinalize = Vector3.zero,
                rotationAtFinalize = Quaternion.identity,
            };
            _tailSegments.Add(seg);
            _activeSegment = seg;
            segGo.transform.position = startPos;
            // Parent stays unit; child will scale along +Y
            segGo.transform.localScale = Vector3.one;
            bodyGo.transform.localPosition = Vector3.zero;
            bodyGo.transform.localRotation = Quaternion.identity;
            bodyGo.transform.localScale = new Vector3(beamWidth, 0.0001f, 1f);
            col.size = new Vector2(1f, 1f); // normalized; scaling via body transform
            col.offset = Vector2.zero; // centered; we'll offset body position for growth
            // Ensure only this active segment's collider is enabled
            foreach (var s in _tailSegments)
            {
                if (s == null || s.collider == null)
                    continue;
                if (s != _activeSegment)
                    s.collider.enabled = false;
                else
                    s.collider.enabled = true;
            }
        }

        private void UpdateAnchoredTailBase() { /* Deprecated: static anchored base (no updates). */
        }

        private void UpdateAnchoredTailVisualization()
        {
            if (_tailParent == null || _activeSegment == null || _pathNodes.Count == 0)
                return;
            // Static anchoring: finalized segments remain exactly where they were created; no refresh needed.
            // (Finalized segment transforms are re-applied each Tick earlier.)
            // Head geometry
            float r = beamWidth * 0.5f; // head radius
            Vector3 headCenter = transform.position + new Vector3(_dir.x, _dir.y, 0f) * _length;
            Vector3 headBase = headCenter - new Vector3(_dir.x, _dir.y, 0f) * r; // point directly behind head

            // Active segment start point
            Vector3 start = _activeSegment.start;
            Vector3 delta = headBase - start;
            float dist = delta.magnitude;

            if (dist < 0.0001f)
            {
                _activeSegment.transform.position = start; // parent pinned
                _activeSegment.transform.rotation = Quaternion.identity;
                if (_activeSegment.body != null)
                {
                    _activeSegment.body.localPosition = Vector3.zero;
                    _activeSegment.body.localRotation = Quaternion.identity;
                    _activeSegment.body.localScale = new Vector3(beamWidth, 0.0001f, 1f);
                }
                return;
            }
            // Parent stays at start; rotate parent so its +Y aims toward headBase
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
            _activeSegment.transform.position = start;
            _activeSegment.transform.rotation = Quaternion.Euler(0, 0, angle);
            if (_activeSegment.body != null)
            {
                // Body scales along +Y from start outward; offset by half length
                _activeSegment.body.localScale = new Vector3(beamWidth, dist, 1f);
                _activeSegment.body.localPosition = new Vector3(0f, dist * 0.5f, 0f);
                if (_activeSegment.collider != null)
                {
                    _activeSegment.collider.size = new Vector2(1f, 1f);
                    _activeSegment.collider.offset = Vector2.zero; // body position handles offset
                }
            }
            if (_activeSegment.sr != null)
            {
                _activeSegment.sr.color = vizColor;
            }
            _activeSegment.end = headBase;
            // Keep endWorldPosition updated for active segment (so if destroyed mid-growth we still have data)
            _activeSegment.endWorldPosition = headBase;
        }

        // Enhanced debug to understand missed hits vs projectile logic parity (call after damage tick if total==0 and debugLogs)
        private void DebugNoHitFrame()
        {
            if (!debugLogs)
                return;
            if (UsingAnchoredTail && _activeSegment != null && _activeSegment.collider != null)
            {
                var b = _activeSegment.collider.bounds;
                Debug.Log(
                    $"[BeamMechanic] No hits. ActiveSeg center={b.center} size={b.size} len={_length} dir={_dir} nodes={_pathNodes.Count}",
                    this
                );
                // Additional diagnostic: physics cross-check using OverlapBox at same pose
                try
                {
                    Vector2 c = b.center;
                    Vector2 s = b.size;
                    var extra = Physics2D.OverlapBoxAll(
                        c,
                        s,
                        _activeSegment.transform.eulerAngles.z,
                        targetLayers
                    );
                    if (extra.Length > 0)
                    {
                        foreach (var e in extra)
                        {
                            Debug.Log(
                                $"[BeamMechanic] CrossCheck OverlapBoxAll saw {e.name} (may have been filtered earlier)",
                                this
                            );
                        }
                    }
                    else
                    {
                        Debug.Log("[BeamMechanic] CrossCheck OverlapBoxAll found none.", this);
                    }
                }
                catch (System.SystemException ex)
                {
                    Debug.LogWarning("[BeamMechanic] CrossCheck exception: " + ex.Message, this);
                }
            }
            else if (!UsingAnchoredTail && _box != null)
            {
                var b = _box.bounds;
                Debug.Log(
                    $"[BeamMechanic] No hits. Box center={b.center} size={b.size} len={_length}",
                    this
                );
            }
        }

        /// <summary>
        /// Invokes BounceMechanic (if present) after a successful damage tick. If bounce chooses destruction
        /// we destroy the beam and return true. Otherwise we redirect in the chosen direction (creating a new
        /// tail segment when anchored) and return false. Only evaluated once per damage tick.
        /// </summary>
        private bool HandleBounceOnHit()
        {
            var bounce = GetComponent<Mechanics.Chaos.BounceMechanic>();
            if (bounce == null)
                return false;
            bool shouldDestroy;
            Vector2 newDir;
            if (!bounce.TryHandleHit(out shouldDestroy, out newDir))
                return false; // no decision (shouldn't happen with current implementation)
            if (shouldDestroy)
            {
                if (debugLogs)
                    Debug.Log("[BeamMechanic] Bounce -> destroy", this);
                Destroy(gameObject);
                return true;
            }
            if (debugLogs)
                Debug.Log("[BeamMechanic] Bounce -> redirect newDir=" + newDir, this);
            Redirect(newDir);
            return false;
        }
    }
}
