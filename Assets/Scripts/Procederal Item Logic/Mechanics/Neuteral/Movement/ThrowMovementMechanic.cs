using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Horizontal throw with simple downward gravity, intended for placing payloads at range.
    [DisallowMultipleComponent]
    public class ThrowMovementMechanic : MonoBehaviour, IMechanic
    {
        [Header("Throw Motion")]
        public Vector2 direction = Vector2.right;
        public float initialSpeed = 12f;
        public float gravity = 22f;
        public float downwardSpeedClamp = 24f;
        public float stopAfterSeconds = 0.65f;

        [Header("Behaviour")]
        public bool alignToVelocity = true;
        public bool zeroVelocityOnStop = true;
        public bool disableOnStop = false;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private Rigidbody2D _rb;
        private Vector2 _velocity;
        private float _timer;
        private bool _stopped;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _velocity = dir * Mathf.Max(0f, initialSpeed);

            _rb = ctx != null ? ctx.PayloadRb2D : null;
            if (_rb == null)
                _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;

            if (alignToVelocity && ctx != null && ctx.Payload != null)
                ctx.Payload.right = dir;

            GameOverController.OnCountdownFinished += StopMovement;

            if (debugLogs)
            {
                Debug.Log(
                    $"[ThrowMovementMechanic] Initialized dir={dir} speed={_velocity.magnitude:F2} stopAfter={stopAfterSeconds:F2}",
                    this
                );
            }
        }

        public void Tick(float dt)
        {
            if (_stopped)
                return;
            if (dt <= 0f)
                return;

            _timer += dt;

            _velocity += Vector2.down * Mathf.Max(0f, gravity) * dt;
            if (downwardSpeedClamp > 0f && _velocity.y < -downwardSpeedClamp)
                _velocity.y = -downwardSpeedClamp;

            Vector2 displacement = _velocity * dt;

            if (_rb != null)
            {
                if (_rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    _rb.linearVelocity = _velocity;
                }
                else
                {
                    _rb.MovePosition(_rb.position + displacement);
                }
            }
            else if (_ctx != null && _ctx.Payload != null)
            {
                _ctx.Payload.position += (Vector3)displacement;
            }
            else
            {
                transform.position += (Vector3)displacement;
            }

            if (
                alignToVelocity
                && _ctx != null
                && _ctx.Payload != null
                && _velocity.sqrMagnitude > 0.0001f
            )
                _ctx.Payload.right = _velocity.normalized;

            if (stopAfterSeconds > 0f && _timer >= stopAfterSeconds)
            {
                if (debugLogs)
                    Debug.Log("[ThrowMovementMechanic] Stop triggered by timer.", this);
                StopMovement();
            }
        }

        private void StopMovement()
        {
            if (_stopped)
                return;
            _stopped = true;
            _velocity = Vector2.zero;

            if (_rb != null)
            {
                if (zeroVelocityOnStop)
                {
                    if (_rb.bodyType == RigidbodyType2D.Dynamic)
                        _rb.linearVelocity = Vector2.zero;
                    else
                        _rb.MovePosition(_rb.position);
                }
                if (disableOnStop)
                    _rb.simulated = false;
            }

            if (disableOnStop)
                enabled = false;

            GameOverController.OnCountdownFinished -= StopMovement;
        }

        private void OnDestroy()
        {
            GameOverController.OnCountdownFinished -= StopMovement;
        }
    }
}
