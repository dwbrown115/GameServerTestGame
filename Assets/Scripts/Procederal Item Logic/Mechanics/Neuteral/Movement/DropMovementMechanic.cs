using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Simple gravity-style movement that accelerates a payload along a direction (default: down)
    /// for a limited time, then stops. Useful for placing payloads on the ground before activating.
    [DisallowMultipleComponent]
    public class DropMovementMechanic : MonoBehaviour, IMechanic
    {
        [Header("Drop Motion")]
        public Vector2 direction = Vector2.down;
        public float initialSpeed = 0f;
        public float gravity = 25f;
        public float maxSpeed = 18f;
        public float stopAfterSeconds = 0.45f;

        [Header("Behaviour")]
        public bool zeroVelocityOnStop = true;
        public bool disableOnStop = true;
        public bool alignToDirection = false;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private Rigidbody2D _rb;
        private Vector2 _dir;
        private float _currentSpeed;
        private float _timer;
        private bool _stopped;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            _currentSpeed = Mathf.Max(0f, initialSpeed);

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

            if (alignToDirection && ctx != null && ctx.Payload != null)
                ctx.Payload.right = _dir;

            GameOverController.OnCountdownFinished += StopMovement;

            if (debugLogs)
            {
                Debug.Log(
                    $"[DropMovementMechanic] Initialized dir={_dir} speed={_currentSpeed:F2} stopAfter={stopAfterSeconds:F2}",
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
            _currentSpeed += Mathf.Max(0f, gravity) * dt;
            if (maxSpeed > 0f)
                _currentSpeed = Mathf.Min(_currentSpeed, maxSpeed);

            Vector2 velocity = _dir * _currentSpeed;
            Vector2 displacement = velocity * dt;

            if (_rb != null)
            {
                if (_rb.bodyType == RigidbodyType2D.Dynamic)
                {
                    _rb.linearVelocity = velocity;
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

            if (stopAfterSeconds > 0f && _timer >= stopAfterSeconds)
            {
                if (debugLogs)
                    Debug.Log("[DropMovementMechanic] Stop triggered by timer.", this);
                StopMovement();
            }
        }

        private void StopMovement()
        {
            if (_stopped)
                return;
            _stopped = true;
            _currentSpeed = 0f;

            if (_rb != null)
            {
                if (zeroVelocityOnStop)
                {
                    if (_rb.bodyType == RigidbodyType2D.Dynamic)
                        _rb.linearVelocity = Vector2.zero;
                    else
                        _rb.MovePosition(_rb.position); // lock in place
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
