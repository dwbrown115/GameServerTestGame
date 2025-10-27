using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Moves a payload at constant velocity for a limited time, then optionally detaches it.
    [DisallowMultipleComponent]
    public class ThrowMovementMechanic : MonoBehaviour, IMechanic
    {
        [Header("Throw Motion")]
        public Vector2 direction = Vector2.right;
        public float initialSpeed = 12f;
        public float stopAfterSeconds = 0.65f;

        [Header("Behaviour")]
        public bool alignToVelocity = true;
        public bool zeroVelocityOnStop = true;
        public bool disableOnStop = false;

        [Tooltip("Detach the payload from its parent once the throw completes.")]
        public bool detachOnStop = true;

        [Tooltip("Detach the payload from its parent as soon as the throw begins.")]
        public bool detachOnStart = true;

        [Tooltip("Randomize the direction on start using a chaos-style spread.")]
        public bool randomizeDirectionOnStart = false;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private Rigidbody2D _rb;
        private float _timer;
        private bool _stopped;
        private Vector2 _moveDir;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;

            if (randomizeDirectionOnStart)
            {
                Vector2 chaos = Random.insideUnitCircle;
                if (chaos.sqrMagnitude < 0.001f)
                    chaos = Vector2.right;
                direction = chaos.normalized;
            }

            _moveDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

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

            if (_rb.bodyType != RigidbodyType2D.Kinematic)
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (alignToVelocity && ctx != null && ctx.Payload != null)
                ctx.Payload.right = _moveDir;

            GameOverController.OnCountdownFinished += StopMovement;

            if (detachOnStart)
                DetachPayload();

            if (debugLogs)
            {
                Debug.Log(
                    $"[ThrowMovementMechanic] Initialized dir={_moveDir} speed={Mathf.Max(0f, initialSpeed):F2} stopAfter={stopAfterSeconds:F2}",
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
            float speed = Mathf.Max(0f, initialSpeed);
            Vector2 displacement = _moveDir * speed * dt;

            if (_rb != null)
            {
                _rb.MovePosition(_rb.position + displacement);
            }
            if (detachOnStop)
                DetachPayload();
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

            if (_rb != null)
            {
                if (zeroVelocityOnStop)
                {
                    _rb.linearVelocity = Vector2.zero;
                    _rb.angularVelocity = 0f;
                    _rb.MovePosition(_rb.position);
                }
                _rb.bodyType = RigidbodyType2D.Kinematic;
                _rb.gravityScale = 0f;
                _rb.Sleep();
                if (disableOnStop)
                    _rb.simulated = false;
            }

            Transform payload = _ctx != null ? _ctx.Payload : transform;
            if (detachOnStop && payload != null && payload.parent != null)
            {
                payload.SetParent(null, worldPositionStays: true);
                if (debugLogs)
                {
                    Debug.Log(
                        "[ThrowMovementMechanic] Detached payload from parent after completing throw.",
                        this
                    );
                }
            }

            if (disableOnStop)
                enabled = false;

            GameOverController.OnCountdownFinished -= StopMovement;
        }

        private void OnDestroy()
        {
            GameOverController.OnCountdownFinished -= StopMovement;
        }

        private void DetachPayload()
        {
            Transform payload = _ctx != null ? _ctx.Payload : transform;
            if (payload == null || payload.parent == null)
                return;

            payload.SetParent(null, worldPositionStays: true);
            if (debugLogs)
            {
                Debug.Log("[ThrowMovementMechanic] Detached payload from parent.", this);
            }
        }
    }
}
