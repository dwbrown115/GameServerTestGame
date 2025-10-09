using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Draws a crescent (annular sector) sweeping from left to right and loops.
    /// Damages targets inside the sector each interval while present. Not destroyed on hit.
    [DisallowMultipleComponent]
    public class WhipMechanic : MonoBehaviour, IMechanic
    {
        [Header("Whip Shape & Motion")]
        [Tooltip("Outer radius (distance from owner) where the crescent is drawn.")]
        [Min(0f)]
        public float outerRadius = 3f;

        [Tooltip("Thickness of the crescent (outerRadius - innerRadius).")]
        [Min(0.01f)]
        public float width = 1f;

        [Tooltip("Arc length of the whip in degrees (span of the crescent).")]
        [Range(5f, 360f)]
        public float arcLengthDeg = 90f;

        [Tooltip("Seconds to sweep from left to right across arcLengthDeg and loop.")]
        [Min(0.05f)]
        public float drawDuration = 1.5f;

        [Header("Damage")]
        [Min(0f)]
        public float interval = 0.1f;
        public int damagePerInterval = 3;
        public LayerMask targetLayers = ~0;
        public bool requireMobTag = true;
        public bool excludeOwner = true;

        [Tooltip(
            "If true, deal damage on trigger enter using an arc band collider instead of interval polling."
        )]
        public bool damageOnTrigger = true;

        [Tooltip(
            "Optional cooldown to avoid repeat hits on the same target while overlapping (seconds)."
        )]
        [Min(0f)]
        public float rehitDelay = 0f;

        [Header("Orientation")]
        [Tooltip("Base direction for whip: right,left,up,down")]
        public string direction = "right";

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = new Color(1f, 0.6f, 0.2f, 0.35f);
        public int vizSortingOrder = -50;

        [Tooltip(
            "When true, draw and damage as a thin outer arc band (outline) instead of a filled crescent."
        )]
        public bool edgeOnly = true;

        [Tooltip("Thickness of the outline band near the outer radius when edgeOnly is true.")]
        [Min(0.01f)]
        public float edgeThickness = 0.25f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _timer;
        private float _tSweep; // 0..1 time through current sweep
        private Vector2 _baseDir = Vector2.right;
        private float _innerRadius => Mathf.Max(0f, outerRadius - width);

        // Viz
        private Transform _vizRoot;
        private Mesh _mesh; // for filled mode
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private LineRenderer _line; // for edgeOnly outline mode

        // Hit collider (for damageOnTrigger)
        private Transform _hitRoot;
        private PolygonCollider2D _hitPoly;
        private Rigidbody2D _hitRb;
        private readonly Dictionary<Collider2D, float> _lastHitTime = new Dictionary<
            Collider2D,
            float
        >(64);

        // Query buffer removed (using List<Collider2D> in DoDamage)

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            _tSweep = 0f;
            _baseDir = ParseDirection(direction);
            if (showVisualization)
                EnsureVisualization();
            if (damageOnTrigger)
                EnsureHitCollider();
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;

            // Sweep time
            float dur = Mathf.Max(0.05f, drawDuration);
            _tSweep += dt / dur;
            if (_tSweep >= 1f)
            {
                _tSweep -= 1f;
            }

            // Compute sector angles (left->right across arcLength)
            float arc = Mathf.Clamp(arcLengthDeg, 5f, 360f);
            // Centered around _baseDir, sweep from -arc/2 to +arc/2 with tSweep window equal to arc/4 width (looks like a whip tip)
            // We model a moving window across the arc: window size = arc/3 for a decent crescent
            float window = Mathf.Max(5f, arc / 3f);
            float startAngle = -arc * 0.5f + (arc - window) * _tSweep;
            float endAngle = startAngle + window;

            // Update visualization
            if (showVisualization)
            {
                UpdateVisualization(startAngle, endAngle);
            }

            if (damageOnTrigger)
            {
                // Update hit collider shape to match current arc window
                UpdateHitCollider(startAngle, endAngle);
            }
            else
            {
                // Damage at intervals to anything within annular sector
                _timer += dt;
                if (_timer >= Mathf.Max(0.01f, interval))
                {
                    _timer = 0f;
                    DoDamage(startAngle, endAngle);
                }
            }
        }

        private void DoDamage(float startDeg, float endDeg)
        {
            Transform centerT = _ctx.Target != null ? _ctx.Target : _ctx.Owner;
            if (centerT == null)
                return;
            Vector2 center = centerT.position;

            // Use a circle overlap for broadphase then filter by annular sector (non-deprecated API)
            var overlaps = new List<Collider2D>(128);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };
            Physics2D.OverlapCircle(center, outerRadius + 0.01f, filter, overlaps);
            int count = overlaps.Count;
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (c == null)
                    continue;
                if (excludeOwner && IsOwnerRelated(c))
                    continue;
                if (requireMobTag && !HasMobTagInParents(c.transform))
                    continue;
                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;

                // Check distance range
                Vector2 p = c.bounds.ClosestPoint(center);
                Vector2 d = p - center;
                float dist = d.magnitude;
                float inner = GetInnerRadius();
                if (dist < inner || dist > outerRadius)
                    continue;

                // Check angle within [start,end] relative to baseDir
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float baseAng = Mathf.Atan2(_baseDir.y, _baseDir.x) * Mathf.Rad2Deg;
                float rel = Mathf.DeltaAngle(baseAng, ang);
                if (rel < startDeg || rel > endDeg)
                    continue;

                Vector2 hitNormal = d.normalized;
                dmg.TakeDamage(damagePerInterval, p, hitNormal);
                total += damagePerInterval;
                if (debugLogs)
                    Debug.Log($"[Whip] Damaged {c.name} for {damagePerInterval}", this);
            }

            // Route to owner Drain if present
            if (total > 0 && _ctx.Owner != null)
            {
                var drain = _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                if (drain != null)
                    drain.ReportDamage(total);
            }
        }

        private void EnsureVisualization()
        {
            if (_vizRoot != null)
                return;
            _vizRoot = new GameObject("WhipViz").transform;
            _vizRoot.SetParent(transform, false);
            _mf = _vizRoot.gameObject.AddComponent<MeshFilter>();
            _mr = _vizRoot.gameObject.AddComponent<MeshRenderer>();
            var sh = Shader.Find("Sprites/Default");
            _mr.sharedMaterial = sh != null ? new Material(sh) : null;
            _mesh = new Mesh();
            _mf.sharedMesh = _mesh;

            // LineRenderer for edge-only mode
            _line = _vizRoot.gameObject.AddComponent<LineRenderer>();
            _line.sharedMaterial = _mr.sharedMaterial;
            _line.sortingOrder = vizSortingOrder;
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.positionCount = 0;
            _line.textureMode = LineTextureMode.Stretch;
        }

        private void EnsureHitCollider()
        {
            if (_hitRoot != null && _hitPoly != null && _hitRb != null)
                return;
            _hitRoot = new GameObject("WhipHit").transform;
            _hitRoot.SetParent(transform, false);
            _hitRoot.localPosition = Vector3.zero;
            _hitRoot.gameObject.layer = gameObject.layer;
            _hitRb = _hitRoot.gameObject.AddComponent<Rigidbody2D>();
            _hitRb.bodyType = RigidbodyType2D.Kinematic;
            _hitRb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _hitRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _hitPoly = _hitRoot.gameObject.AddComponent<PolygonCollider2D>();
            _hitPoly.isTrigger = true;
            // Relay trigger events upwards
            var relay = _hitRoot.gameObject.AddComponent<PayloadTriggerRelay>();
            relay.debugLogs = debugLogs;
        }

        private void UpdateVisualization(float startDeg, float endDeg)
        {
            if (_vizRoot == null)
                EnsureVisualization();
            float baseAng = Mathf.Atan2(_baseDir.y, _baseDir.x) * Mathf.Rad2Deg;
            int segments = Mathf.Clamp(Mathf.CeilToInt((endDeg - startDeg) / 6f), 3, 128);
            if (edgeOnly)
            {
                // Outline: render a single arc near the outer radius
                if (_line == null)
                    _line = _vizRoot.gameObject.AddComponent<LineRenderer>();
                _line.enabled = true;
                _line.sortingOrder = vizSortingOrder;
                _line.startColor = vizColor;
                _line.endColor = vizColor;
                float lw = Mathf.Max(0.01f, edgeThickness);
                _line.startWidth = lw;
                _line.endWidth = lw;
                _line.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float a = Mathf.Lerp(startDeg, endDeg, t) + baseAng;
                    float rad = a * Mathf.Deg2Rad;
                    float cx = Mathf.Cos(rad);
                    float cy = Mathf.Sin(rad);
                    _line.SetPosition(i, new Vector3(cx * outerRadius, cy * outerRadius, 0f));
                }
                // Hide mesh renderer in outline mode
                if (_mr != null)
                    _mr.enabled = false;
            }
            else
            {
                // Filled crescent mesh
                if (_mesh == null)
                    _mesh = new Mesh();
                float inner = GetInnerRadius();
                float outer = outerRadius;
                int vertCount = (segments + 1) * 2;
                var verts = new Vector3[vertCount];
                var cols = new Color[vertCount];
                var tris = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float a = Mathf.Lerp(startDeg, endDeg, t) + baseAng;
                    float rad = a * Mathf.Deg2Rad;
                    float cx = Mathf.Cos(rad);
                    float cy = Mathf.Sin(rad);
                    verts[i * 2 + 0] = new Vector3(cx * inner, cy * inner, 0f);
                    verts[i * 2 + 1] = new Vector3(cx * outer, cy * outer, 0f);
                    cols[i * 2 + 0] = vizColor;
                    cols[i * 2 + 1] = vizColor;
                }
                int ti = 0;
                for (int i = 0; i < segments; i++)
                {
                    int i0 = i * 2;
                    int i1 = i * 2 + 1;
                    int i2 = i * 2 + 2;
                    int i3 = i * 2 + 3;
                    tris[ti++] = i0;
                    tris[ti++] = i2;
                    tris[ti++] = i1;
                    tris[ti++] = i1;
                    tris[ti++] = i2;
                    tris[ti++] = i3;
                }
                _mesh.Clear();
                _mesh.vertices = verts;
                _mesh.colors = cols;
                _mesh.triangles = tris;
                _mesh.RecalculateBounds();
                _mesh.RecalculateNormals();
                // Show mesh renderer, hide line
                if (_mr != null)
                {
                    _mr.enabled = true;
                    _mr.sortingOrder = vizSortingOrder;
                }
                if (_line != null)
                {
                    _line.enabled = false;
                }
            }
        }

        private void UpdateHitCollider(float startDeg, float endDeg)
        {
            if (_hitPoly == null)
                return;
            float baseAng = Mathf.Atan2(_baseDir.y, _baseDir.x) * Mathf.Rad2Deg;
            int segments = Mathf.Clamp(Mathf.CeilToInt((endDeg - startDeg) / 6f), 3, 128);
            float outer = outerRadius;
            float inner = GetInnerRadius();
            // Build points: outer arc (start->end), then inner arc (end->start)
            var pts = new Vector2[(segments + 1) * 2];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(startDeg, endDeg, t) + baseAng;
                float rad = a * Mathf.Deg2Rad;
                float cx = Mathf.Cos(rad);
                float cy = Mathf.Sin(rad);
                pts[i] = new Vector2(cx * outer, cy * outer);
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(endDeg, startDeg, t) + baseAng;
                float rad = a * Mathf.Deg2Rad;
                float cx = Mathf.Cos(rad);
                float cy = Mathf.Sin(rad);
                pts[(segments + 1) + i] = new Vector2(cx * inner, cy * inner);
            }
            _hitPoly.pathCount = 1;
            _hitPoly.SetPath(0, pts);
        }

        // Trigger-based damage (relayed by PayloadTriggerRelay)
        private void OnPayloadTriggerEnter2D(Collider2D other)
        {
            if (!damageOnTrigger || other == null)
                return;
            if (((1 << other.gameObject.layer) & targetLayers) == 0)
                return;
            if (excludeOwner && IsOwnerRelated(other))
                return;
            if (requireMobTag && !HasMobTagInParents(other.transform))
                return;
            if (_lastHitTime.TryGetValue(other, out var last) && rehitDelay > 0f)
            {
                if (Time.time - last < rehitDelay)
                    return;
            }
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive)
                return;
            Vector2 center =
                _ctx?.Target != null ? (Vector2)_ctx.Target.position : (Vector2)transform.position;
            Vector2 p = other.bounds.ClosestPoint(center);
            Vector2 n = (p - center).sqrMagnitude > 1e-6f ? (p - center).normalized : Vector2.up;
            dmg.TakeDamage(damagePerInterval, p, n);
            _lastHitTime[other] = Time.time;
            if (debugLogs)
                Debug.Log($"[Whip] Trigger hit {other.name} for {damagePerInterval}", this);
            // Drain report
            if (_ctx?.Owner != null)
            {
                var drain = _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                if (drain != null)
                    drain.ReportDamage(damagePerInterval);
            }
        }

        private void OnPayloadTriggerStay2D(Collider2D other)
        {
            if (!damageOnTrigger || other == null)
                return;
            if (rehitDelay <= 0f)
                return;
            if (((1 << other.gameObject.layer) & targetLayers) == 0)
                return;
            if (excludeOwner && IsOwnerRelated(other))
                return;
            if (requireMobTag && !HasMobTagInParents(other.transform))
                return;
            if (!_lastHitTime.TryGetValue(other, out var last))
                last = -999f;
            if (Time.time - last < rehitDelay)
                return;
            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive)
                return;
            Vector2 center =
                _ctx?.Target != null ? (Vector2)_ctx.Target.position : (Vector2)transform.position;
            Vector2 p = other.bounds.ClosestPoint(center);
            Vector2 n = (p - center).sqrMagnitude > 1e-6f ? (p - center).normalized : Vector2.up;
            dmg.TakeDamage(damagePerInterval, p, n);
            _lastHitTime[other] = Time.time;
            if (debugLogs)
                Debug.Log($"[Whip] Trigger stay hit {other.name} for {damagePerInterval}", this);
            if (_ctx?.Owner != null)
            {
                var drain = _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                if (drain != null)
                    drain.ReportDamage(damagePerInterval);
            }
        }

        private void OnPayloadTriggerExit2D(Collider2D other)
        {
            if (other == null)
                return;
            _lastHitTime.Remove(other);
        }

        private float GetInnerRadius()
        {
            if (edgeOnly)
                return Mathf.Max(0f, outerRadius - Mathf.Max(0.01f, edgeThickness));
            return Mathf.Max(0f, outerRadius - width);
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
    }
}
