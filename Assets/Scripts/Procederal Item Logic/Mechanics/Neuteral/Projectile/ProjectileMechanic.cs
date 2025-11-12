using Game.Procederal.Core;
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
        private Mechanics.Neuteral.ChildMovementMechanic _childMovement;
        private Mechanics.Neuteral.ThrowMovementMechanic _throwMovement;
        private Mechanics.Neuteral.DropMovementMechanic _dropMovement;

        // Reusable list for generic modifier dispatch (no concrete modifier references)
        private static System.Collections.Generic.List<Mechanics.IPrimaryHitModifier> _hitMods =
            new System.Collections.Generic.List<Mechanics.IPrimaryHitModifier>(8);

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;
            direction.Normalize();
            EnsureVisualAndPhysics();
            CacheMovementControllers();
            SyncMovementFromControllers();
            ApplyDirectionToControllers(
                direction,
                alignPayload: true,
                refreshSpeedFromChild: false
            );
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
                        // Only add the default circle if this payload explicitly requested it.
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

        private void CacheMovementControllers()
        {
            _childMovement = GetComponent<Mechanics.Neuteral.ChildMovementMechanic>();
            _throwMovement = GetComponent<Mechanics.Neuteral.ThrowMovementMechanic>();
            _dropMovement = GetComponent<Mechanics.Neuteral.DropMovementMechanic>();
        }

        private bool HasExternalMovementController()
        {
            return _childMovement != null || _throwMovement != null || _dropMovement != null;
        }

        private void SyncMovementFromControllers()
        {
            // If a child movement controller exists, prefer its configured direction/speed
            if (_childMovement != null)
            {
                try
                {
                    var dir = _childMovement.direction;
                    if (dir.sqrMagnitude > 0.0001f)
                        direction = dir.normalized;
                    speed = Mathf.Max(0f, _childMovement.speed);
                }
                catch { }
                return;
            }

            if (_throwMovement != null)
            {
                try
                {
                    var dir = _throwMovement.direction;
                    if (dir.sqrMagnitude > 0.0001f)
                        direction = dir.normalized;
                    speed = Mathf.Max(0f, _throwMovement.initialSpeed);
                }
                catch { }
                return;
            }

            if (_dropMovement != null)
            {
                try
                {
                    var dir = _dropMovement.direction;
                    if (dir.sqrMagnitude > 0.0001f)
                        direction = dir.normalized;
                    speed = Mathf.Max(0f, _dropMovement.initialSpeed);
                }
                catch { }
            }
        }

        private void ApplyDirectionToControllers(
            Vector2 dir,
            bool alignPayload,
            bool refreshSpeedFromChild
        )
        {
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            dir.Normalize();

            if (alignPayload && _ctx != null && _ctx.Payload != null)
            {
                _ctx.Payload.right = dir;
            }

            if (_childMovement != null)
            {
                _childMovement.direction = dir;
                if (refreshSpeedFromChild)
                    speed = Mathf.Max(0f, _childMovement.speed);
            }
            if (_throwMovement != null)
            {
                _throwMovement.direction = dir;
                if (refreshSpeedFromChild)
                    speed = Mathf.Max(0f, _throwMovement.initialSpeed);
            }
            if (_dropMovement != null)
            {
                _dropMovement.direction = dir;
                if (refreshSpeedFromChild)
                    speed = Mathf.Max(0f, _dropMovement.initialSpeed);
            }
        }

        public void Tick(float dt)
        {
            if (_ctx == null || _ctx.Payload == null)
                return;
            if (_stopped)
                return;
            if (disableSelfSpeed || HasExternalMovementController())
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

            // Removed direct early modifier invocations (Lock/Ripple). Generic hit dispatch occurs only after confirmed damage.

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
                if (debugLogs)
                    Debug.Log($"[ProjectileMechanic] Damaged {other.name} for {damage}", this);
                // Generic primary-hit dispatch (replaces explicit modifier calls)
                DispatchPrimaryHit(other.transform, hitPoint, hitNormal, damage);

                // Bounce redirect decision (special-case: directional change / destruction control)
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
                                MechanicLifecycleUtility.Release(
                                    _ctx.Payload.gameObject,
                                    immediate: false
                                );
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
                        MechanicLifecycleUtility.Release(_ctx.Payload.gameObject, immediate: false);
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

        private void DispatchPrimaryHit(
            Transform target,
            Vector2 hitPoint,
            Vector2 hitNormal,
            int dmg
        )
        {
            _hitMods.Clear();
            GetComponents(_hitMods);
            var info = new Mechanics.PrimaryHitInfo(target, hitPoint, hitNormal, dmg, this);
            for (int i = 0; i < _hitMods.Count; i++)
            {
                var mod = _hitMods[i];
                if (mod == null)
                    continue;
                try
                {
                    mod.OnPrimaryHit(in info);
                }
                catch (System.Exception ex)
                {
                    if (debugLogs)
                        Debug.LogWarning(
                            $"[ProjectileMechanic] Modifier exception: {ex.Message}",
                            this
                        );
                }
            }
        }
    }
}
