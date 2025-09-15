using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Moves the payload forward at a constant speed; if a Rigidbody2D is present, uses velocity.
    public class ProjectileMechanic : MonoBehaviour, IMechanic
    {
        [Header("Projectile Settings")]
        public Vector2 direction = Vector2.right;
        public float speed = 5f;
        public int damage = 10;

        [Tooltip(
            "If true, only colliders tagged 'Mob' (or with a parent tagged 'Mob') will be damaged."
        )]
        public bool requireMobTag = true;

        [Tooltip("If true, collisions against the owner or its children are ignored.")]
        public bool excludeOwner = true;

        [Tooltip("If true, destroys the payload on hit")]
        public bool destroyOnHit = true;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;
            direction.Normalize();
        }

        public void Tick(float dt)
        {
            if (_ctx == null || _ctx.Payload == null)
                return;

            if (_ctx.PayloadRb2D != null)
            {
                _ctx.PayloadRb2D.linearVelocity = direction * speed;
            }
            else
            {
                _ctx.Payload.position += (Vector3)(direction * speed * dt);
            }
        }

        // Called by PayloadTriggerRelay on the host; mechanics can implement to react to collisions
        private void OnPayloadTriggerEnter2D(Collider2D other)
        {
            if (debugLogs && other != null)
                Debug.Log($"[ProjectileMechanic] ENTER with {other.name} tag={other.tag}", this);
            HandleTriggerHit(other);
        }

        private void OnPayloadTriggerStay2D(Collider2D other)
        {
            if (debugLogs && other != null)
                Debug.Log($"[ProjectileMechanic] STAY with {other.name} tag={other.tag}", this);
            HandleTriggerHit(other);
        }

        private void HandleTriggerHit(Collider2D other)
        {
            if (_ctx == null || other == null)
                return;

            // Ignore collisions with the owner or its hierarchy/attached bodies
            if (excludeOwner && _ctx.Owner != null && IsOwnerRelated(other))
            {
                if (debugLogs)
                    Debug.Log("[ProjectileMechanic] Ignored owner collision", this);
                return;
            }

            // Filter to mobs if required
            if (requireMobTag)
            {
                if (!HasMobTagInParents(other.transform))
                {
                    if (debugLogs)
                        Debug.Log(
                            $"[ProjectileMechanic] Non-mob hit: {other.name} tag={other.tag}",
                            this
                        );
                    return;
                }
            }

            // Damage any IDamageable on this or parent
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null && other.attachedRigidbody != null)
                damageable = other.attachedRigidbody.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                // Compute hit point and normal consistent with IDamageable signature
                Vector2 attackerPos =
                    _ctx.Payload != null
                        ? (Vector2)_ctx.Payload.position
                        : (
                            _ctx.Owner != null
                                ? (Vector2)_ctx.Owner.position
                                : (Vector2)transform.position
                        );
                Vector2 hitPoint = other.ClosestPoint(attackerPos);
                Vector2 hitNormal = ((Vector2)other.transform.position - attackerPos).normalized;
                damageable.TakeDamage(damage, hitPoint, hitNormal);
                if (debugLogs)
                    Debug.Log($"[ProjectileMechanic] Damaged {other.name} for {damage}", this);

                if (destroyOnHit)
                {
                    if (_ctx.Payload != null)
                    {
                        if (debugLogs)
                            Debug.Log(
                                "[ProjectileMechanic] Destroying payload on hit (destroyOnHit=true)",
                                this
                            );
                        Object.Destroy(_ctx.Payload.gameObject);
                    }
                    else if (debugLogs)
                    {
                        Debug.Log(
                            "[ProjectileMechanic] destroyOnHit=true but no payload reference; skipping destroy",
                            this
                        );
                    }
                }
            }
            else if (debugLogs)
            {
                Debug.Log(
                    $"[ProjectileMechanic] IDamageable not found or not alive on {other.name}",
                    this
                );
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
