using System.Collections.Generic;
using Game.Procederal.Api;
using UnityEngine;

namespace Mechanics.Chaos
{
    /// Spawns an expanding ring (ripple) at the hit position when the payload hits an enemy.
    /// If that ripple touches a different enemy, deals damage and rolls a chance to spawn
    /// another ripple at the new enemy, chaining up to maxChains.
    [DisallowMultipleComponent]
    public class RippleOnHitMechanic : MonoBehaviour, IMechanic, Mechanics.IPrimaryHitModifier
    {
        [Header("Ripple Settings")]
        [Tooltip("Starting radius of the ring (world units)")]
        [Min(0f)]
        public float startRadius = 0.5f;

        [Tooltip("Final diameter of the ring (world units). Final radius = endDiameter/2.")]
        [Min(0.01f)]
        public float endDiameter = 6f;

        [Tooltip("Seconds to grow from startRadius to endDiameter/2")]
        [Min(0.05f)]
        public float growDuration = 0.6f;

        [Tooltip("Thickness of the ring edge for touch detection")]
        [Min(0.01f)]
        public float edgeThickness = 0.2f;

        [Tooltip("Damage dealt by each ripple instance on touch")]
        public int rippleDamage = 5;

        [Header("Chaining")]
        [Range(0f, 1f)]
        public float chainChance = 0.5f;

        [Min(0)]
        public int maxChains = 3;

        [Header("Filters")]
        public LayerMask targetLayers = ~0;
        public bool requireMobTag = true;
        public bool excludeOwner = true;

        [Tooltip("If false, colliders that belong to the Player (tag 'Player') are ignored.")]
        public bool includePlayer = false;

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        public int vizSortingOrder = -40;

        [Header("Debug")]
        public bool debugLogs = false;

        [Tooltip("Detailed lifecycle & chain debugging")]
        public bool traceLifecycle = false;

        [Header("Parenting")]
        [Tooltip(
            "If true, the first ripple (and chained ripples) will be parented to the hit target or chained target instead of this mechanic so ripples organize under mobs. World position is preserved."
        )]
        public bool parentToTarget = true;

        [Header("Center & Follow")]
        [Tooltip(
            "If true, the ripple will spawn centered exactly on the target's current position (ignores contact point)."
        )]
        public bool centerOnTarget = false;

        [Tooltip(
            "If true and parentToTarget is enabled, ripple keeps recentering on the target every Tick (follows moving target). If false, it stays at initial spawn position."
        )]
        public bool followTargetWhileGrowing = false;

        private MechanicContext _ctx;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(
            float dt
        ) { /* no per-frame work on host */
        }

        /// Entry point from primaries: call when an enemy is hit to start the chain.
        public void TriggerFrom(Transform initialTarget, Vector2 hitPoint)
        {
            if (_ctx == null)
                return;
            if (debugLogs || traceLifecycle)
                Debug.Log(
                    $"[RippleOnHit] Trigger at {hitPoint} initial={initialTarget?.name} maxChains={maxChains}",
                    this
                );
            SpawnRippleChain(hitPoint, initialTarget, maxChains);
        }

        // IPrimaryHitModifier: trigger ripple chain from any primary hit
        public void OnPrimaryHit(in Mechanics.PrimaryHitInfo info)
        {
            TriggerFrom(info.target, info.hitPoint);
        }

        private void SpawnRippleChain(Vector2 center, Transform excludeTransform, int remaining)
        {
            var go = new GameObject("RippleChainInstance");
            Transform chosenParent = transform;
            if (parentToTarget && excludeTransform != null)
            {
                chosenParent = excludeTransform;
            }
            go.transform.SetParent(chosenParent, worldPositionStays: true);
            // Optionally override center to target transform position for exact centering
            Vector3 spawnCenter = center;
            if (centerOnTarget && excludeTransform != null)
            {
                spawnCenter = excludeTransform.position;
            }
            go.transform.position = spawnCenter;
            go.layer = gameObject.layer;
            var inst = go.AddComponent<RippleChainInstance>();
            // Configure
            inst.parent = this;
            inst.owner = _ctx != null ? _ctx.Owner : null;
            inst.excludeTransform = excludeTransform;
            inst.remainingChains = Mathf.Max(0, remaining);
            inst.startRadius = Mathf.Max(0f, startRadius);
            inst.endRadius = Mathf.Max(0.005f, endDiameter * 0.5f);
            inst.growDuration = Mathf.Max(0.05f, growDuration);
            inst.edgeThickness = Mathf.Max(0.01f, edgeThickness);
            inst.rippleDamage = Mathf.Max(0, rippleDamage);
            inst.targetLayers = targetLayers;
            inst.requireMobTag = requireMobTag;
            inst.excludeOwner = excludeOwner;
            inst.includePlayer = includePlayer;
            inst.showVisualization = showVisualization;
            inst.vizColor = vizColor;
            inst.vizSortingOrder = vizSortingOrder;
            inst.debugLogs = debugLogs;
            inst.followTarget = followTargetWhileGrowing && parentToTarget;
            inst.anchorTarget =
                (centerOnTarget || followTargetWhileGrowing) ? excludeTransform : null;
            // Safety: if we ended up at world origin unintentionally but have a target, snap now
            if (inst.anchorTarget != null && go.transform.position == Vector3.zero)
            {
                go.transform.position = inst.anchorTarget.position;
                if (traceLifecycle || debugLogs)
                    Debug.Log(
                        "[RippleOnHit] Corrected spawn position from (0,0,0) to anchor target position",
                        this
                    );
            }
            // Bring into the ticking loop
            var runner = GetComponentInParent<MechanicRunner>();
            if (runner != null)
            {
                if (traceLifecycle)
                    Debug.Log(
                        $"[RippleOnHit] Registering ripple instance with runner={runner.name} parent={(chosenParent != null ? chosenParent.name : "null")}",
                        this
                    );
                runner.RegisterTree(runner.transform);
            }
            else if (traceLifecycle)
            {
                Debug.Log(
                    "[RippleOnHit] No MechanicRunner found in parents; ripple may not Tick",
                    this
                );
            }
        }

        private void ContinueChainFrom(Transform newTarget, Vector2 newCenter, int remaining)
        {
            if (remaining <= 0)
                return;
            float roll = Random.value;
            if (roll <= chainChance)
            {
                if (debugLogs || traceLifecycle)
                    Debug.Log(
                        $"[RippleOnHit] Chain success roll={roll:0.###} <= {chainChance:0.###} -> {newTarget?.name} rem={remaining - 1}",
                        this
                    );
                SpawnRippleChain(newCenter, newTarget, remaining - 1);
            }
            else if (debugLogs || traceLifecycle)
            {
                Debug.Log(
                    $"[RippleOnHit] Chain fail roll={roll:0.###} > {chainChance:0.###}",
                    this
                );
            }
        }

        // Lightweight inner worker that behaves similarly to RippleMechanic but also calls back to parent
        private class RippleChainInstance : MonoBehaviour, IMechanic
        {
            [HideInInspector]
            public RippleOnHitMechanic parent;

            [HideInInspector]
            public Transform owner;

            [HideInInspector]
            public Transform excludeTransform;

            [HideInInspector]
            public int remainingChains;

            [HideInInspector]
            public float startRadius;

            [HideInInspector]
            public float endRadius;

            [HideInInspector]
            public float growDuration;

            [HideInInspector]
            public float edgeThickness;

            [HideInInspector]
            public int rippleDamage;

            [HideInInspector]
            public LayerMask targetLayers;

            [HideInInspector]
            public bool requireMobTag;

            [HideInInspector]
            public bool excludeOwner;

            [HideInInspector]
            public bool includePlayer; // if false we ignore colliders on objects tagged Player

            [HideInInspector]
            public bool showVisualization;

            [HideInInspector]
            public Color vizColor;

            [HideInInspector]
            public int vizSortingOrder;

            [HideInInspector]
            public bool debugLogs;

            // Following behavior
            [HideInInspector]
            public bool followTarget;

            [HideInInspector]
            public Transform anchorTarget;

            private float _t;
            private float _radius;
            private readonly HashSet<Collider2D> _hit = new();
            private LineRenderer _line;
            private SpriteRenderer _spriteRing;
            private bool _chained;
            private bool _selfTick;

            public void Initialize(
                MechanicContext ctx
            ) { /* not required */
            }

            public void Tick(float dt)
            {
                // Manual update via runner's Tick
                if (growDuration <= 0.0001f)
                {
                    if (debugLogs)
                        Debug.Log(
                            "[RippleOnHit:Instance] growDuration too small, auto-destroy",
                            this
                        );
                    Destroy(gameObject);
                    return;
                }
                if (_t <= 0f)
                {
                    _radius = Mathf.Max(0f, startRadius);
                    if (showVisualization)
                        EnsureViz();
                    UpdateViz();
                    if (debugLogs)
                        Debug.Log(
                            $"[RippleOnHit:Instance] Start radius={_radius:0.###} endRadius={endRadius:0.###} growth={growDuration:0.###}",
                            this
                        );
                }

                // Optional follow: recenter on target each frame before damage/overlap
                if (followTarget && anchorTarget != null)
                {
                    Vector3 before = transform.position;
                    transform.position = anchorTarget.position;
                    if (debugLogs && (before - transform.position).sqrMagnitude > 0.0001f)
                    {
                        Debug.Log(
                            $"[RippleOnHit:Instance] Follow recenter from {before} to {transform.position}",
                            this
                        );
                    }
                }
                _t += dt;
                float n = Mathf.Clamp01(_t / Mathf.Max(0.01f, growDuration));
                _radius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0.005f, endRadius), n);
                if (showVisualization)
                    UpdateViz();
                DoOnTouchDamage();
                if (n >= 1f)
                {
                    if (debugLogs)
                        Debug.Log("[RippleOnHit:Instance] Completed growth; destroying", this);
                    Destroy(gameObject);
                }
            }

            private void Update()
            {
                // Fallback self tick if no MechanicRunner registered us (rare). We approximate dt with Time.deltaTime.
                if (_selfTick)
                {
                    Tick(Time.deltaTime);
                }
            }

            private void OnEnable()
            {
                // If no parent runner exists we will self-tick
                var runner = GetComponentInParent<MechanicRunner>();
                if (runner == null)
                {
                    _selfTick = true;
                    if (debugLogs)
                        Debug.Log(
                            "[RippleOnHit:Instance] No MechanicRunner found; enabling self-tick Update loop",
                            this
                        );
                }
            }

            private void EnsureViz()
            {
                if (_line != null || _spriteRing != null)
                    return;
                // Prefer line; fallback to simple sprite circle if shader/material missing
                var sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    _line = gameObject.AddComponent<LineRenderer>();
                    var mr = gameObject.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = new Material(sh);
                    _line.sharedMaterial = mr.sharedMaterial;
                    _line.useWorldSpace = true;
                    _line.loop = true;
                    _line.sortingOrder = vizSortingOrder;
                    _line.positionCount = 64;
                }
                else
                {
                    _spriteRing = gameObject.AddComponent<SpriteRenderer>();
                    _spriteRing.sortingOrder = vizSortingOrder;
                    _spriteRing.color = vizColor;
                    // Attempt to reuse any unit circle sprite from generator if accessible
                    var circle = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                    _spriteRing.sprite = circle;
                }
            }

            private void UpdateViz()
            {
                if (_line != null)
                {
                    _line.startColor = vizColor;
                    _line.endColor = vizColor;
                    float lw = Mathf.Max(0.01f, edgeThickness);
                    _line.startWidth = lw;
                    _line.endWidth = lw;
                    int seg = 64;
                    _line.positionCount = seg;
                    for (int i = 0; i < seg; i++)
                    {
                        float a = (i / (float)seg) * Mathf.PI * 2f;
                        float x = Mathf.Cos(a) * _radius;
                        float y = Mathf.Sin(a) * _radius;
                        _line.SetPosition(
                            i,
                            new Vector3(transform.position.x + x, transform.position.y + y, 0f)
                        );
                    }
                }
                else if (_spriteRing != null)
                {
                    // Scale sprite outward (sprite assumed unit diameter ≈ 1)
                    float targetDiameter = Mathf.Max(0.01f, _radius * 2f);
                    _spriteRing.transform.localScale = new Vector3(
                        targetDiameter,
                        targetDiameter,
                        1f
                    );
                    _spriteRing.color = vizColor;
                }
            }

            private void DoOnTouchDamage()
            {
                Vector2 center = transform.position;
                float bandHalf = Mathf.Max(0.005f, edgeThickness * 0.5f);
                float queryR = _radius + bandHalf + 0.25f;
                var overlaps = new List<Collider2D>(64);
                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = targetLayers,
                    useTriggers = true,
                };
                Physics2D.OverlapCircle(center, queryR, filter, overlaps);
                if (debugLogs)
                    Debug.Log(
                        $"[RippleOnHit:Instance] Overlap count={overlaps.Count} radius={_radius:0.###} query={queryR:0.###}",
                        this
                    );

                int total = 0;
                Transform nextTarget = null;
                Vector2 nextCenter = Vector2.zero;
                foreach (var c in overlaps)
                {
                    if (c == null || _hit.Contains(c))
                        continue;
                    if (excludeOwner && IsOwnerRelated(c))
                        continue;
                    if (!includePlayer && IsPlayerRelated(c))
                        continue;
                    if (requireMobTag && !HasMobTagInParents(c.transform))
                        continue;
                    // Skip the original target for this link
                    if (
                        excludeTransform != null
                        && (
                            c.transform == excludeTransform
                            || c.transform.IsChildOf(excludeTransform)
                        )
                    )
                        continue;

                    var dmg = c.GetComponentInParent<IDamageable>();
                    if (dmg == null || !dmg.IsAlive)
                        continue;
                    Vector2 p = c.bounds.ClosestPoint(center);
                    Vector2 d = p - center;
                    float dist = d.magnitude;
                    if (Mathf.Abs(dist - _radius) > bandHalf)
                        continue;
                    Vector2 n = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
                    dmg.TakeDamage(rippleDamage, p, n);
                    _hit.Add(c);
                    total += rippleDamage;
                    if (debugLogs)
                        Debug.Log(
                            $"[RippleOnHit:Instance] Damaged {c.name} for {rippleDamage}",
                            this
                        );

                    // Capture first valid to chain
                    if (!_chained && nextTarget == null)
                    {
                        nextTarget = c.transform;
                        nextCenter = p;
                    }
                }

                if (total > 0 && owner != null)
                {
                    var drain = owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                    if (drain != null)
                        drain.ReportDamage(total);
                }

                if (!_chained && nextTarget != null && parent != null)
                {
                    _chained = true; // ensure we only attempt once
                    parent.ContinueChainFrom(nextTarget, nextCenter, remainingChains);
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
                if (owner == null || c == null)
                    return false;
                var o = owner;
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

            private bool IsPlayerRelated(Collider2D c)
            {
                if (c == null)
                    return false;
                // Accept either direct tag on collider's transform or any parent.
                Transform t = c.transform;
                while (t != null)
                {
                    if (t.CompareTag("Player"))
                        return true;
                    t = t.parent;
                }
                if (c.attachedRigidbody != null)
                {
                    var rt = c.attachedRigidbody.transform;
                    if (rt.CompareTag("Player"))
                        return true;
                }
                return false;
            }
        }
    }
}
