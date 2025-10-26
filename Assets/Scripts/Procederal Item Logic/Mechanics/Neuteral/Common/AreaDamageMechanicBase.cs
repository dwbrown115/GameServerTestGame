using Mechanics.Chaos;
using Mechanics.Corruption;
using Mechanics.Order;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Shared helper for circular area damage mechanics (Aura, Damage Zone, etc.).
    /// Handles collider setup, ticking, target filtering, and common modifier hooks.
    public abstract class AreaDamageMechanicBase : MonoBehaviour, IMechanic
    {
        [Header("Area Damage")]
        public float radius = 2f;
        public int damagePerInterval = 1;
        public float interval = 0.5f;
        public LayerMask targetLayers = ~0;

        [Header("Target Filters")]
        public bool excludeOwner = true;
        public bool requireMobTag = true;

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = new Color(0.85f, 0.85f, 0.85f, 0.25f);
        public int vizSortingOrder = -100;

        [Header("Debug")]
        public bool debugLogs = false;

        public virtual Color spriteColor
        {
            get => vizColor;
            set => vizColor = value;
        }

        protected MechanicContext Context { get; private set; }
        protected Vector2 CurrentCenter { get; private set; }

        protected readonly Collider2D[] hitsBuffer = new Collider2D[32];
        protected float tickTimer;

        private CircleCollider2D _queryCollider;
        private Transform _queryRoot;
        private ContactFilter2D _filter;

        private Transform _vizRoot;
        private SpriteRenderer _vizSr;
        private Sprite _vizSprite;

        private DrainMechanic _drain;
        private LockMechanic _lock;
        private DamageOverTimeMechanic _dot;
        private ExplosionMechanic _explosion;
        private RippleOnHitMechanic _ripple;

        protected CircleCollider2D QueryCollider => _queryCollider;

        public virtual void Initialize(MechanicContext ctx)
        {
            Context = ctx;
            tickTimer = 0f;

            CacheOptionalMechanics();
            SetupQueryCollider();
            ConfigureFilter();
            OnAfterInitialize();

            Vector2 center = ResolveCenter();
            CurrentCenter = center;
            UpdateQueryTransform(center);

            if (showVisualization)
            {
                EnsureVisualization();
                UpdateVisualization(center, 0f);
            }
            else
            {
                DisableVisualization();
            }
        }

        public virtual void Tick(float dt)
        {
            if (Context == null)
                return;

            Vector2 center = ResolveCenter();
            CurrentCenter = center;
            UpdateQueryTransform(center);

            if (showVisualization)
            {
                EnsureVisualization();
                UpdateVisualization(center, dt);
            }
            else
            {
                DisableVisualization();
            }

            if (!PreTickUpdate(dt, center))
                return;

            if (!TryConsumeTickWindow(dt))
                return;

            int hitCount =
                (_queryCollider != null) ? _queryCollider.Overlap(_filter, hitsBuffer) : 0;

            if (debugLogs)
                Debug.Log($"[{DebugLabel}] Overlap count={hitCount} center={center}", this);

            if (hitCount <= 0)
                return;

            bool anyDamaged = false;
            int totalDamage = 0;

            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitsBuffer[i];
                if (collider == null)
                    continue;
                if (ShouldSkipCollider(collider))
                    continue;
                if (
                    !TryResolveDamageTarget(collider, center, out var damageable, out var direction)
                )
                    continue;

                int amount = GetDamageAmount(collider, damageable);
                if (amount <= 0)
                    continue;

                damageable.TakeDamage(amount, direction, Vector2.zero);
                totalDamage += amount;
                anyDamaged = true;

                ApplyOnHitModifiers(collider.transform);
                OnDamageApplied(damageable, collider, center);
            }

            if (anyDamaged)
            {
                OnTargetsDamaged(totalDamage, center);
            }
        }

        protected virtual void OnAfterInitialize() { }

        protected virtual bool PreTickUpdate(float dt, Vector2 center) => true;

        protected virtual bool TryConsumeTickWindow(float dt)
        {
            tickTimer += dt;
            if (tickTimer < Mathf.Max(0.0001f, interval))
                return false;
            tickTimer = 0f;
            return true;
        }

        protected void ResetTickTimer() => tickTimer = 0f;

        protected virtual Vector2 ResolveCenter()
        {
            if (Context != null)
            {
                if (Context.Payload != null)
                    return Context.Payload.position;
                if (Context.Owner != null)
                    return Context.Owner.position;
            }
            return transform.position;
        }

        protected virtual string QueryTransformName => "AreaDamageQuery";
        protected virtual string VisualizationRootName => "AreaDamageViz";
        protected virtual string DebugLabel => GetType().Name;

        protected virtual bool ShouldSkipCollider(Collider2D collider)
        {
            if (collider == null)
                return true;
            if (excludeOwner && IsOwnerRelated(collider))
                return true;
            if (requireMobTag && !HasMobTagInParents(collider.transform))
                return true;
            return false;
        }

        protected virtual bool TryResolveDamageTarget(
            Collider2D collider,
            Vector2 center,
            out IDamageable damageable,
            out Vector2 direction
        )
        {
            damageable = collider.GetComponentInParent<IDamageable>();
            direction = ((Vector2)collider.transform.position - center).normalized;
            return damageable != null;
        }

        protected virtual int GetDamageAmount(Collider2D collider, IDamageable target)
        {
            return Mathf.Max(0, damagePerInterval);
        }

        protected virtual void OnDamageApplied(
            IDamageable damageable,
            Collider2D sourceCollider,
            Vector2 center
        ) { }

        protected virtual void OnTargetsDamaged(int totalDamage, Vector2 center)
        {
            if (_drain != null && totalDamage > 0)
                _drain.ReportDamage(totalDamage);

            if (_explosion != null)
                _explosion.TriggerExplosion(center);

            if (_ripple != null)
                _ripple.TriggerFrom(null, center);
        }

        protected bool IsOwnerRelated(Collider2D collider)
        {
            if (Context == null || Context.Owner == null || collider == null)
                return false;

            var owner = Context.Owner;
            if (
                collider.transform == owner
                || collider.transform.IsChildOf(owner)
                || owner.IsChildOf(collider.transform)
            )
                return true;

            if (collider.attachedRigidbody != null)
            {
                var rbTransform = collider.attachedRigidbody.transform;
                if (
                    rbTransform == owner
                    || rbTransform.IsChildOf(owner)
                    || owner.IsChildOf(rbTransform)
                )
                    return true;
            }

            return collider.transform.root == owner.root;
        }

        protected static bool HasMobTagInParents(Transform t)
        {
            while (t != null)
            {
                if (t.CompareTag("Mob"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        protected void DestroySelf()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }

        protected void ForceDisableVisualization()
        {
            showVisualization = false;
            if (_vizRoot != null && _vizRoot.gameObject.activeSelf)
                _vizRoot.gameObject.SetActive(false);
        }

        private void CacheOptionalMechanics()
        {
            _drain = GetComponent<DrainMechanic>();
            _lock = GetComponent<LockMechanic>();
            _dot = GetComponent<DamageOverTimeMechanic>();
            _explosion = GetComponent<ExplosionMechanic>();
            _ripple = GetComponent<RippleOnHitMechanic>();
        }

        private void SetupQueryCollider()
        {
            string nodeName = QueryTransformName;
            _queryRoot = transform.Find(nodeName);
            if (_queryRoot == null)
            {
                var go = new GameObject(nodeName);
                _queryRoot = go.transform;
                _queryRoot.SetParent(transform, false);
            }

            _queryCollider = _queryRoot.GetComponent<CircleCollider2D>();
            if (_queryCollider == null)
                _queryCollider = _queryRoot.gameObject.AddComponent<CircleCollider2D>();
            _queryCollider.isTrigger = true;
            _queryCollider.radius = 0.5f;
            _queryCollider.enabled = true;
            UpdateQueryScale();
        }

        private void ConfigureFilter()
        {
            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };
        }

        protected void RefreshTargetLayers()
        {
            ConfigureFilter();
        }

        private void UpdateQueryTransform(Vector2 center)
        {
            if (_queryRoot == null)
                return;
            _queryRoot.position = center;
            UpdateQueryScale();
        }

        private void UpdateQueryScale()
        {
            if (_queryRoot == null)
                return;
            float scale = Mathf.Max(0f, radius * 2f);
            _queryRoot.localScale = new Vector3(scale, scale, 1f);
        }

        private void EnsureVisualization()
        {
            if (_vizRoot != null)
                return;

            _vizRoot = new GameObject(VisualizationRootName).transform;
            _vizRoot.SetParent(transform, false);
            _vizSr = _vizRoot.gameObject.AddComponent<SpriteRenderer>();
            _vizSr.sprite = GetVizSprite();
            _vizSr.sortingOrder = vizSortingOrder;
            _vizSr.color = vizColor;
        }

        private void DisableVisualization()
        {
            if (_vizRoot != null && _vizRoot.gameObject.activeSelf)
                _vizRoot.gameObject.SetActive(false);
        }

        private void UpdateVisualization(Vector2 center, float dt)
        {
            if (_vizRoot == null)
                return;

            if (!_vizRoot.gameObject.activeSelf)
                _vizRoot.gameObject.SetActive(true);

            float diameter = Mathf.Max(0f, radius * 2f);
            _vizRoot.position = center;
            _vizRoot.localScale = new Vector3(diameter, diameter, 1f);

            if (_vizSr != null)
            {
                _vizSr.color = vizColor;
                _vizSr.sortingOrder = vizSortingOrder;
            }

            OnVisualizationUpdated(dt, center, diameter);
        }

        protected virtual void OnVisualizationUpdated(float dt, Vector3 center, float diameter) { }

        private Sprite GetVizSprite()
        {
            if (_vizSprite != null)
                return _vizSprite;

            _vizSprite = CreateWhiteCircleSprite(64);
            return _vizSprite;
        }

        private static Sprite CreateWhiteCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            int cx = size / 2;
            int cy = size / 2;
            float rad = size * 0.5f - 0.5f;
            float r2 = rad * rad;
            var colors = new Color[size * size];
            Color on = Color.white;
            Color off = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx + 0.5f;
                    float dy = y - cy + 0.5f;
                    int idx = y * size + x;
                    colors[idx] = (dx * dx + dy * dy) <= r2 ? on : off;
                }
            }
            tex.SetPixels(colors);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void ApplyOnHitModifiers(Transform target)
        {
            if (_lock != null)
                _lock.TryApplyTo(target);
            if (_dot != null)
                _dot.TryApplyTo(target);
        }
    }
}
