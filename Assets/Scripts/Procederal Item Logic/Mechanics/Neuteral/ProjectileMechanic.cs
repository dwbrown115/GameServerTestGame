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

        [Tooltip("Disable self movement; Orbit controls motion.")]
        public bool disableSelfSpeed = false;

        [Tooltip(
            "If true, only colliders tagged 'Mob' (or with a parent tagged 'Mob') will be damaged."
        )]
        public bool requireMobTag = true;

        [Tooltip("If true, collisions against the owner or its children are ignored.")]
        public bool excludeOwner = true;

        [Tooltip("If true, destroys the payload on hit")]
        public bool destroyOnHit = true;

        [Header("Visual (optional)")]
        [Tooltip("circle | square | custom (loads Resources path in customSpritePath)")]
        public string spriteType = null;
        public string customSpritePath = null;
        public Color spriteColor = Color.white;
        public bool autoAddPhysicsBody = true;

        [Header("Debug")]
        public bool debugLogs = false;

        [Tooltip("Extra verbose ripple path logs")]
        public bool debugRipple = false;

        private MechanicContext _ctx;
        private bool _stopped;
        private bool _rippleTriggered;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;
            direction.Normalize();
            EnsureVisualAndPhysics();
            GameOverController.OnCountdownFinished += StopMovement;
        }

        private void EnsureVisualAndPhysics()
        {
            // Add sprite renderer only if none exists and a spriteType is provided
            if (!string.IsNullOrWhiteSpace(spriteType) && GetComponent<SpriteRenderer>() == null)
            {
                var sr = gameObject.AddComponent<SpriteRenderer>();
                Sprite chosen = null;
                switch ((spriteType ?? "circle").ToLowerInvariant())
                {
                    case "custom":
                        if (!string.IsNullOrEmpty(customSpritePath))
                            chosen = Resources.Load<Sprite>(customSpritePath);
                        if (chosen == null)
                            chosen = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                    case "square":
                        chosen = Game.Procederal.ProcederalItemGenerator.GetUnitSquareSprite();
                        break;
                    case "circle":
                    default:
                        chosen = Game.Procederal.ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                }
                sr.sprite = chosen;
                sr.color = spriteColor;
            }

            // Collider + RB (if missing) so projectile can interact; keep minimal if orbit disables motion
            if (GetComponent<Collider2D>() == null)
            {
                var cc = gameObject.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 0.5f;
            }
            if (autoAddPhysicsBody && GetComponent<Rigidbody2D>() == null)
            {
                var rbChild = gameObject.AddComponent<Rigidbody2D>();
                rbChild.bodyType = RigidbodyType2D.Kinematic;
                rbChild.interpolation = RigidbodyInterpolation2D.Interpolate;
                rbChild.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        public void Tick(float dt)
        {
            if (_ctx == null || _ctx.Payload == null)
                return;
            if (_stopped)
                return;
            // If another mechanic (e.g., Orbit) controls movement, do not modify velocity/position here.
            if (disableSelfSpeed)
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

        private void OnDestroy()
        {
            GameOverController.OnCountdownFinished -= StopMovement;
            _rippleTriggered = false;
        }

        private void StopMovement()
        {
            _stopped = true;
            if (_ctx != null && _ctx.PayloadRb2D != null)
                _ctx.PayloadRb2D.linearVelocity = Vector2.zero;
        }

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

        // Direct Unity trigger hooks so we don't depend on relays
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (debugLogs && other != null)
                Debug.Log(
                    $"[ProjectileMechanic] OnTriggerEnter2D with {other.name} tag={other.tag}",
                    this
                );
            HandleTriggerHit(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (debugLogs && other != null)
                Debug.Log(
                    $"[ProjectileMechanic] OnTriggerStay2D with {other.name} tag={other.tag}",
                    this
                );
            HandleTriggerHit(other);
        }

        private void HandleTriggerHit(Collider2D other)
        {
            if (_ctx == null || other == null)
                return;

            if (excludeOwner && _ctx.Owner != null && IsOwnerRelated(other))
            {
                if (debugLogs)
                    Debug.Log("[ProjectileMechanic] Ignored owner collision", this);
                return;
            }

            if (requireMobTag)
            {
                if (!HasMobTagInParents(other.transform))
                {
                    if (debugLogs || debugRipple)
                        Debug.Log(
                            $"[ProjectileMechanic] Non-mob hit (mobTag required) collider={other.name} tag={other.tag}",
                            this
                        );
                    return;
                }
            }

            // Attempt to apply stun early (independent of damageability)
            var earlyLocker = GetComponent<Mechanics.Order.LockMechanic>();
            if (earlyLocker != null)
            {
                if (earlyLocker.debugLogs && other != null)
                    Debug.Log($"[ProjectileMechanic] Early Lock attempt on {other.name}", this);
                earlyLocker.TryApplyTo(other.transform);
            }

            // Early ripple trigger (independent of damageability) so ripple works on objects without IDamageable
            var earlyRipple = GetComponent<Mechanics.Chaos.RippleOnHitMechanic>();
            if (debugRipple)
            {
                Debug.Log(
                    $"[ProjectileMechanic] Ripple check early: comp={(earlyRipple != null)} triggered={_rippleTriggered}",
                    this
                );
            }
            if (earlyRipple != null && !_rippleTriggered)
            {
                Vector2 origin =
                    _ctx.Payload != null
                        ? (Vector2)_ctx.Payload.position
                        : (Vector2)transform.position;
                Vector2 rp = other.ClosestPoint(origin);
                earlyRipple.TriggerFrom(other.transform, rp);
                _rippleTriggered = true;
                if (debugLogs || debugRipple)
                    Debug.Log(
                        $"[ProjectileMechanic] Early ripple trigger at {rp} via {other.name}",
                        this
                    );
            }

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null && other.attachedRigidbody != null)
                damageable = other.attachedRigidbody.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
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
                // Route damage to Drain if present on the same root
                if (_ctx.Owner != null)
                {
                    var drain =
                        _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                    if (drain != null)
                        drain.ReportDamage(damage);
                }
                if (debugLogs)
                    Debug.Log($"[ProjectileMechanic] Damaged {other.name} for {damage}", this);

                // Explosion modifier: trigger radial damage from the hit point (affects players and mobs)
                var explode = GetComponent<Mechanics.Chaos.ExplosionMechanic>();
                if (explode != null)
                {
                    explode.TriggerExplosion(hitPoint);
                }

                // Ripple already triggered early; leave legacy path for safety if not yet triggered
                var rippleOnHit = GetComponent<Mechanics.Chaos.RippleOnHitMechanic>();
                if (debugRipple)
                {
                    Debug.Log(
                        $"[ProjectileMechanic] Ripple check late: comp={(rippleOnHit != null)} triggered={_rippleTriggered}",
                        this
                    );
                }
                if (rippleOnHit != null && !_rippleTriggered)
                {
                    rippleOnHit.TriggerFrom(other.transform, hitPoint);
                    _rippleTriggered = true;
                    if (debugLogs || debugRipple)
                        Debug.Log(
                            $"[ProjectileMechanic] Late ripple trigger at {hitPoint} via {other.name}",
                            this
                        );
                }

                // Apply Lock modifier if present on this payload
                var locker = GetComponent<Mechanics.Order.LockMechanic>();
                if (locker != null)
                {
                    locker.TryApplyTo(other.transform);
                }

                // Apply DamageOverTime modifier if present on this payload
                var dot = GetComponent<Mechanics.Corruption.DamageOverTimeMechanic>();
                if (dot != null)
                    dot.TryApplyTo(other.transform);

                // Bounce modifier handling takes precedence over simple destroyOnHit
                var bounce = GetComponent<Mechanics.Chaos.BounceMechanic>();
                if (bounce != null)
                {
                    if (bounce.TryHandleHit(out bool shouldDestroy, out Vector2 newDir))
                    {
                        if (shouldDestroy)
                        {
                            if (_ctx.Payload != null)
                            {
                                if (debugLogs)
                                    Debug.Log("[ProjectileMechanic] Bounce decided destroy", this);
                                Object.Destroy(_ctx.Payload.gameObject);
                            }
                        }
                        else
                        {
                            // apply new direction and continue moving
                            direction = newDir;
                            if (_ctx.PayloadRb2D != null)
                                _ctx.PayloadRb2D.linearVelocity = direction * speed;
                        }
                    }
                }
                else if (destroyOnHit)
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
