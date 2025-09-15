using UnityEngine;

namespace Mechanics.Corruption
{
    /// Drains life from enemies within a radius: damages them and heals the owner by a fraction.
    public class DrainMechanic : MonoBehaviour, IMechanic
    {
        [Header("Drain Settings")]
        public float radius = 2f;
        public int damagePerInterval = 1;
        public float interval = 0.5f;

        [Range(0f, 1f)]
        public float lifeStealRatio = 0.5f; // 50% of damage dealt
        public LayerMask targetLayers = ~0;

        [Tooltip("Center on target instead of owner if true and target exists")]
        public bool centerOnTarget = false;

        private MechanicContext _ctx;
        private float _timer;
        private Collider2D[] _hits = new Collider2D[32];
        private PlayerHealth _ownerHealth;
        private CircleCollider2D _queryCollider;
        private ContactFilter2D _filter;

        [Header("Debug")]
        public bool debugLogs = false;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            // Prefer the context owner; fallback to a global Player tag
            if (_ctx?.Owner != null)
                _ownerHealth = _ctx.Owner.GetComponent<PlayerHealth>();
            if (_ownerHealth == null)
            {
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                    _ownerHealth = playerGo.GetComponent<PlayerHealth>();
            }

            _queryCollider = gameObject.AddComponent<CircleCollider2D>();
            _queryCollider.isTrigger = true;
            _queryCollider.radius = radius;
            _queryCollider.enabled = false;

            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true,
            };
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;
            _timer += dt;
            if (_timer < interval)
                return;
            _timer = 0f;

            Vector2 center =
                (_ctx != null && _ctx.Target != null && centerOnTarget && _ctx.Owner != null)
                    ? (Vector2)_ctx.Target.position
                    : (Vector2)(_ctx?.Owner != null ? _ctx.Owner.position : transform.position);
            _queryCollider.radius = radius;
            _queryCollider.transform.position = center;
            _queryCollider.enabled = true;
            int count = _queryCollider.Overlap(_filter, _hits);
            _queryCollider.enabled = false;

            float totalDamage = 0f;
            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (c == null)
                    continue;
                // Ignore owner-related colliders (owner or its hierarchy/attached bodies)
                if (IsOwnerRelated(c))
                {
                    if (debugLogs)
                        Debug.Log(
                            $"[DrainMechanic] Ignored owner-related collider {c.name}.",
                            this
                        );
                    continue;
                }
                // Ignore player-owned colliders
                if (c.CompareTag("Player") || c.GetComponent<PlayerHealth>() != null)
                    continue;

                var dmg = c.GetComponent<IDamageable>();
                if (dmg == null)
                    continue;
                Vector2 dir = ((Vector2)c.transform.position - center).normalized;
                dmg.TakeDamage(damagePerInterval, dir, Vector2.zero);
                totalDamage += damagePerInterval;
            }

            if (totalDamage > 0f)
            {
                HealOwnerFromDamage((int)totalDamage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0f, 1f, 0.4f);
            var center = transform.position;
            Gizmos.DrawWireSphere(center, radius);
        }

        // Allow other mechanics (e.g., Aura) to route dealt-damage for lifesteal
        public void HealOwnerFromDamage(int totalDamage)
        {
            if (_ownerHealth == null || totalDamage <= 0 || lifeStealRatio <= 0f)
                return;
            int heal = Mathf.RoundToInt(totalDamage * lifeStealRatio);
            if (heal > 0)
            {
                _ownerHealth.Heal(heal);
                if (debugLogs)
                    Debug.Log(
                        $"[DrainMechanic] Healed owner {heal} from routed damage {totalDamage} (ratio={lifeStealRatio:F2})",
                        this
                    );
            }
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
