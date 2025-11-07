using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Expanding circular outline (ring) that damages once when it touches a mob.
    [DisallowMultipleComponent]
    public class RippleMechanic : MonoBehaviour, IMechanic
    {
        [Header("Ripple Shape & Motion")]
        [Tooltip("Starting radius of the ring (world units) from the center (owner/target).")]
        [Min(0f)]
        public float startRadius = 1f;

        [Tooltip("Final diameter of the ring (world units). Final radius = endDiameter/2.")]
        [Min(0.01f)]
        public float endDiameter = 8f;

        [Tooltip(
            "Seconds for the ring to grow from startRadius to endDiameter/2, then self-destroy."
        )]
        [Min(0.01f)]
        public float growDuration = 1.5f;

        [Tooltip("Thickness of the damage band around the ring.")]
        [Min(0.01f)]
        public float edgeThickness = 0.2f;

        [Header("Damage & Filters")]
        public int damage = 5;
        public LayerMask targetLayers = ~0;
        public bool requireMobTag = true;
        public bool excludeOwner = true;

        [Tooltip("If false, colliders on the Player (tag 'Player') are ignored by the ripple.")]
        public bool includePlayer = false;

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        public int vizSortingOrder = -40;

        [Range(12, 256)]
        public int segments = 64;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _t;
        private float _radius;
        private float _endRadius;
        private readonly HashSet<Collider2D> _hit = new();

        // viz
        private Transform _vizRoot;
        private LineRenderer _line;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _t = 0f;
            _radius = Mathf.Max(0f, startRadius);
            _endRadius = Mathf.Max(0.0001f, endDiameter * 0.5f);
            if (showVisualization)
                EnsureViz();
            UpdateViz();
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;
            _t += dt;
            float dur = Mathf.Max(0.01f, growDuration);
            float n = Mathf.Clamp01(_t / dur);
            _radius = Mathf.Lerp(Mathf.Max(0f, startRadius), _endRadius, n);
            if (showVisualization)
                UpdateViz();
            DoOnTouchDamage();
            if (n >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void EnsureViz()
        {
            if (_vizRoot != null)
                return;
            _vizRoot = new GameObject("RippleViz").transform;
            _vizRoot.SetParent(transform, false);
            _line = _vizRoot.gameObject.AddComponent<LineRenderer>();
            var sh = Shader.Find("Sprites/Default");
            var mr = _vizRoot.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sh != null ? new Material(sh) : null;
            _line.sharedMaterial = mr.sharedMaterial;
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.sortingOrder = vizSortingOrder;
        }

        private void UpdateViz()
        {
            if (_line == null)
                return;
            int seg = Mathf.Clamp(segments, 12, 256);
            _line.positionCount = seg;
            float lw = Mathf.Max(0.01f, edgeThickness);
            _line.startWidth = lw;
            _line.endWidth = lw;
            _line.startColor = vizColor;
            _line.endColor = vizColor;
            for (int i = 0; i < seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * _radius;
                float y = Mathf.Sin(a) * _radius;
                _line.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private void DoOnTouchDamage()
        {
            Vector2 center = ResolveCenter();
            float bandHalf = Mathf.Max(0.005f, edgeThickness * 0.5f);
            float queryR = _radius + bandHalf + 0.25f; // pad to catch AABB corners
            var overlaps = new List<Collider2D>(64);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };
            Physics2D.OverlapCircle(center, queryR, filter, overlaps);
            int total = 0;
            bool triggeredRippleOnHitThisTick = false;
            foreach (var c in overlaps)
            {
                if (c == null)
                    continue;
                if (_hit.Contains(c))
                    continue; // already hit by this ripple
                if (excludeOwner && IsOwnerRelated(c))
                    continue;
                if (!includePlayer && IsPlayerRelated(c))
                    continue;
                if (requireMobTag && !HasMobTagInParents(c.transform))
                    continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;
                Vector2 p = c.bounds.ClosestPoint(center);
                Vector2 d = p - center;
                float dist = d.magnitude;
                if (Mathf.Abs(dist - _radius) > bandHalf)
                    continue; // must be near the ring edge
                Vector2 n = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
                dmg.TakeDamage(damage, p, n);
                _hit.Add(c);
                total += damage;
                if (debugLogs)
                    Debug.Log($"[Ripple] Damaged {c.name} for {damage}", this);

                // If a RippleOnHit modifier is present on this payload, trigger a chain once per tick from the first new target touched
                if (!triggeredRippleOnHitThisTick)
                {
                    var rippleOnHit = GetComponent<Mechanics.Chaos.RippleOnHitMechanic>();
                    if (rippleOnHit != null)
                    {
                        rippleOnHit.TriggerFrom(c.transform, p);
                        triggeredRippleOnHitThisTick = true;
                    }
                }
            }
            if (total > 0 && _ctx.Owner != null)
            {
                var drain = _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                if (drain != null)
                    drain.ReportDamage(total);
            }
        }

        private Vector2 ResolveCenter()
        {
            if (_ctx != null)
            {
                if (_ctx.Payload != null)
                    return _ctx.Payload.position;
                if (_ctx.Target != null)
                    return _ctx.Target.position;
                if (_ctx.Owner != null)
                    return _ctx.Owner.position;
            }

            return transform.position;
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

        private bool IsPlayerRelated(Collider2D c)
        {
            if (c == null)
                return false;
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
