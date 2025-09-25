using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Shoots a forward beam that expands lengthwise (tombstone shape: flat base + half-circle tip),
    /// dealing continuous damage at intervals to targets within the beam until it reaches maxDistance.
    public class BeamMechanic : MonoBehaviour, IMechanic
    {
        [Header("Beam Shape & Motion")]
        [Tooltip("World width of the beam (does not expand).")]
        [Min(0f)]
        public float beamWidth = 1f;

        [Tooltip("Max forward length the beam will reach before being destroyed.")]
        [Min(0f)]
        public float maxDistance = 8f;

        [Tooltip("Units per second the beam extends forward along its direction.")]
        [Min(0f)]
        public float extendSpeed = 20f;

        [Tooltip(
            "Direction string: right,left,up,down or angle degrees (e.g., 45). Defaults to right."
        )]
        public string direction = "right";

        [Header("Damage")]
        [Min(0f)]
        public float interval = 0.1f; // seconds between damage ticks

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

        private MechanicContext _ctx;
        private float _timer;
        private float _length; // current length achieved
        private Vector2 _dir = Vector2.right; // normalized
        private ContactFilter2D _filter;
        private Collider2D[] _hits = new Collider2D[64];

        // Hit shape
        private Transform _hitRoot;
        private BoxCollider2D _box;
        private CircleCollider2D _tip;

        // Viz
        private Transform _vizRoot;
        private SpriteRenderer _rectSr;
        private SpriteRenderer _tipSr;
        private Sprite _circleSprite;
        private Sprite _squareSprite;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            _length = 0f;
            _dir = ParseDirection(direction);

            // Create hit root rotated to face direction; local +Y is forward
            _hitRoot = (new GameObject("BeamHit")).transform;
            _hitRoot.SetParent(transform, false);
            _hitRoot.localPosition = Vector3.zero;
            _hitRoot.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                new Vector3(_dir.x, _dir.y, 0f)
            );

            _box = _hitRoot.gameObject.AddComponent<BoxCollider2D>();
            _box.isTrigger = true;
            _tip = _hitRoot.gameObject.AddComponent<CircleCollider2D>();
            _tip.isTrigger = true;

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };

            if (showVisualization)
                EnsureVisualization();

            UpdateGeometry();
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;

            // Extend
            if (_length < maxDistance)
            {
                _length = Mathf.Min(maxDistance, _length + extendSpeed * dt);
                UpdateGeometry();
            }

            // Damage tick
            _timer += dt;
            if (_timer >= Mathf.Max(0.01f, interval))
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
                // Report to Drain if present on owner
                if (totalDamage > 0 && _ctx.Owner != null)
                {
                    var drain =
                        _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                    if (drain != null)
                        drain.ReportDamage(totalDamage);
                }

                // Bounce modifier integration: if we hit anything this tick, decide whether to destroy or bounce
                var bounce = GetComponent<Mechanics.Chaos.BounceMechanic>();
                if (bounce != null && totalDamage > 0)
                {
                    if (bounce.TryHandleHit(out bool shouldDestroy, out Vector2 newDir))
                    {
                        if (shouldDestroy)
                        {
                            if (debugLogs)
                                Debug.Log("[BeamMechanic] Bounce decided destroy", this);
                            Destroy(gameObject);
                            return;
                        }
                        // Re-aim the beam in the new direction and reset its length to extend again
                        _dir = newDir.sqrMagnitude > 1e-6f ? newDir.normalized : _dir;
                        if (_hitRoot != null)
                        {
                            _hitRoot.localRotation = Quaternion.FromToRotation(
                                Vector3.up,
                                new Vector3(_dir.x, _dir.y, 0f)
                            );
                        }
                        _length = 0f;
                        UpdateGeometry();
                    }
                }
            }

            // Lifetime end
            if (_length >= maxDistance)
            {
                // Optionally wait one more small frame for visuals; destroy now for simplicity
                Destroy(gameObject);
            }
        }

        private int DoDamageTick()
        {
            if (_box == null || _tip == null)
                return 0;
            int total = 0;

            // Gather from box
            int count = _box.Overlap(_filter, _hits);
            total += DamageHits(count);

            // Gather from tip, but avoid double-hits by reusing the buffer and applying again (harmless to re-hit same target)
            count = _tip.Overlap(_filter, _hits);
            total += DamageHits(count);

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
            float rectLen = Mathf.Max(0f, _length - r);

            // Box centered at half its length forward along local +Y
            _box.size = new Vector2(w, Mathf.Max(0.0001f, rectLen));
            _box.offset = new Vector2(0f, rectLen * 0.5f);

            // Tip circle sits at end
            _tip.radius = r;
            _tip.offset = new Vector2(0f, rectLen + r);

            if (showVisualization)
                UpdateVisualization(w, rectLen, r);
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

            // Separate children so each sprite has its own transform
            var rectGo = new GameObject("Rect");
            rectGo.transform.SetParent(_vizRoot, false);
            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(_vizRoot, false);

            _rectSr = rectGo.AddComponent<SpriteRenderer>();
            _tipSr = tipGo.AddComponent<SpriteRenderer>();

            if (_rectSr != null)
            {
                _rectSr.sortingOrder = vizSortingOrder;
                _rectSr.color = vizColor;
            }
            if (_tipSr != null)
            {
                _tipSr.sortingOrder = vizSortingOrder;
                _tipSr.color = vizColor;
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
            if (_tipSr != null)
                _tipSr.sprite = _circleSprite;
        }

        private void UpdateVisualization(float w, float rectLen, float r)
        {
            if (_vizRoot == null)
                EnsureVisualization();
            if (_rectSr != null)
            {
                _rectSr.enabled = rectLen > 0.0001f;
                _rectSr.color = vizColor;
                _rectSr.sortingOrder = vizSortingOrder;
                _rectSr.transform.localPosition = new Vector3(0f, rectLen * 0.5f, 0f);
                _rectSr.transform.localScale = new Vector3(w, rectLen, 1f);
            }
            if (_tipSr != null)
            {
                _tipSr.enabled = r > 0f;
                _tipSr.color = vizColor;
                _tipSr.sortingOrder = vizSortingOrder;
                _tipSr.transform.localPosition = new Vector3(0f, rectLen + r, 0f);
                _tipSr.transform.localScale = new Vector3(w, w, 1f);
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
    }
}
