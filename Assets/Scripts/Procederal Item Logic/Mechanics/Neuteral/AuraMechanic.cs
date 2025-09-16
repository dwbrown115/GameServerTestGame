using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Damages enemies within a radius around owner/target at fixed intervals.
    public class AuraMechanic : MonoBehaviour, IMechanic
    {
        [Header("Aura Settings")]
        public float radius = 2f;
        public int damagePerInterval = 1;
        public float interval = 0.5f;
        public LayerMask targetLayers = ~0;

        [Tooltip("Center on target instead of owner if true and target exists")]
        public bool centerOnTarget = false;

        [Tooltip("Skip colliders tagged 'Player' or with PlayerHealth")]
        public bool excludePlayer = true;

        [Tooltip("Only damage colliders tagged 'Mob' (or their parents)")]
        public bool requireMobTag = true;

        [Header("Visualization")]
        [Tooltip("Show a light gray transparent circle indicating the aura radius at runtime")]
        public bool showVisualization = true;
        public Color vizColor = new Color(0.85f, 0.85f, 0.85f, 0.25f);
        public int vizSortingOrder = -100;

        // Alias to allow Primary JSON key 'spriteColor' to map to visualization color
        public Color spriteColor
        {
            get => vizColor;
            set => vizColor = value;
        }

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _timer;
        private Collider2D[] _hits = new Collider2D[32];
        private CircleCollider2D _queryCollider; // used only for OverlapCollider
        private ContactFilter2D _filter;
        private Transform _vizRoot;
        private SpriteRenderer _vizSr;
        private Sprite _vizSprite;
        private Vector3 _lastVizPos;
        private float _lastVizDiameter;
        private float _vizLogTimer;
        private Mechanics.Corruption.DrainMechanic _drain;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            _drain = GetComponent<Mechanics.Corruption.DrainMechanic>();
            // Use a dedicated child for the query collider to avoid conflicts with other mechanics (e.g., Drain)
            var queryRoot = transform.Find("AuraQuery");
            if (queryRoot == null)
            {
                var go = new GameObject("AuraQuery");
                queryRoot = go.transform;
                queryRoot.SetParent(transform, false);
            }
            _queryCollider = queryRoot.GetComponent<CircleCollider2D>();
            if (_queryCollider == null)
                _queryCollider = queryRoot.gameObject.AddComponent<CircleCollider2D>();
            _queryCollider.isTrigger = true;
            // Use 0.5 local radius and scale the transform so world radius == this.radius
            // worldRadius = localRadius * scale => 0.5 * (2*radius) = radius
            _queryCollider.radius = 0.5f;
            _queryCollider.enabled = true; // keep enabled; we use OverlapCollider explicitly
            queryRoot.localScale = new Vector3(
                Mathf.Max(0f, radius * 2f),
                Mathf.Max(0f, radius * 2f),
                1f
            );

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };

            if (showVisualization)
            {
                EnsureVisualization();
                UpdateVisualization();
                if (debugLogs)
                    Debug.Log("[AuraMechanic] Visualization initialized.", this);
            }
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;
            _timer += dt;
            if (showVisualization)
            {
                if (_vizRoot == null)
                    EnsureVisualization();
                if (_vizRoot != null && !_vizRoot.gameObject.activeSelf)
                    _vizRoot.gameObject.SetActive(true);
                if (_vizRoot != null)
                    UpdateVisualization();
                if (debugLogs)
                {
                    _vizLogTimer += dt;
                    if (_vizLogTimer >= 0.5f)
                    {
                        _vizLogTimer = 0f;
                        Debug.Log(
                            $"[AuraMechanic] Viz pos={_lastVizPos} diameter={_lastVizDiameter:F2} radius={radius:F2}",
                            this
                        );
                    }
                }
            }
            else if (_vizRoot != null && _vizRoot.gameObject.activeSelf)
            {
                _vizRoot.gameObject.SetActive(false);
            }

            if (_timer < interval)
                return;
            _timer = 0f;

            Vector2 center =
                (_ctx.Target != null && centerOnTarget)
                    ? (Vector2)_ctx.Target.position
                    : (Vector2)_ctx.Owner.position;
            // Keep collider unit radius and scale to match desired radius
            var qr = _queryCollider.transform;
            qr.position = center;
            qr.localScale = new Vector3(Mathf.Max(0f, radius * 2f), Mathf.Max(0f, radius * 2f), 1f);
            int count = _queryCollider.Overlap(_filter, _hits);
            if (debugLogs)
                Debug.Log(
                    $"[AuraMechanic] Overlap count={count} at center={center} radius={radius}",
                    this
                );

            int totalDamage = 0;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c == null)
                    continue;
                if (IsOwnerRelated(c))
                {
                    if (debugLogs)
                        Debug.Log($"[AuraMechanic] Ignored owner-related collider {c.name}.", this);
                    continue;
                }
                if (excludePlayer)
                {
                    if (c.CompareTag("Player") || c.GetComponentInParent<PlayerHealth>() != null)
                        continue;
                }
                if (requireMobTag && !HasMobTagInParents(c.transform))
                {
                    continue;
                }

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null)
                    continue;
                Vector2 dir = ((Vector2)c.transform.position - center).normalized;
                dmg.TakeDamage(damagePerInterval, dir, Vector2.zero);
                totalDamage += damagePerInterval;
                if (debugLogs)
                    Debug.Log(
                        $"[AuraMechanic] Damaged {c.name} -> {dmg.GetType().Name} for {damagePerInterval}",
                        this
                    );
            }

            if (_drain != null && totalDamage > 0)
            {
                _drain.ReportDamage(totalDamage);
            }
        }

        private void EnsureVisualization()
        {
            if (_vizRoot != null)
                return;
            _vizRoot = new GameObject("AuraViz").transform;
            _vizRoot.SetParent(transform, false);
            _vizSr = _vizRoot.gameObject.AddComponent<SpriteRenderer>();
            _vizSr.sortingOrder = vizSortingOrder;
            _vizSr.color = vizColor;
            if (_vizSprite == null)
                _vizSprite = GenerateWhiteCircleSprite(64);
            _vizSr.sprite = _vizSprite;
        }

        private void UpdateVisualization()
        {
            Vector3 center =
                (_ctx != null && _ctx.Target != null && centerOnTarget)
                    ? _ctx.Target.position
                    : transform.position;
            if (_vizRoot == null)
                return;
            _vizRoot.position = center;
            // Scale uses diameter for visualization for consistency with collider scaling
            float s = Mathf.Max(0f, radius * 2f);
            _vizRoot.localScale = new Vector3(s, s, 1f);
            _lastVizPos = center;
            _lastVizDiameter = s;
            if (_vizSr != null)
            {
                _vizSr.color = vizColor;
                _vizSr.sortingOrder = vizSortingOrder;
            }
        }

        private Sprite GenerateWhiteCircleSprite(int size)
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
                    int idx = y * size + x;
                    colors[idx] = (dx * dx + dy * dy) <= r2 ? on : off;
                }
            }
            tex.SetPixels(colors);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            var center = transform.position;
            Gizmos.DrawWireSphere(center, radius);
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
