using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Generic child movement controller for procedurally spawned payloads.
    /// - Moves along a direction at constant speed
    /// - If a Rigidbody2D exists, uses linearVelocity; otherwise translates the transform
    /// - Honors disableSelfSpeed so other mechanics (e.g., Orbit) can take over motion
    /// This intentionally contains no hit/damage logic; combine with other mechanics for effects.
    [DisallowMultipleComponent]
    public class ChildMovementMechanic : MonoBehaviour, IMechanic
    {
        [Header("Movement")]
        public Vector2 direction = Vector2.right;
        public float speed = 5f;

        [Tooltip(
            "Disable internal movement updates; allows external controllers like Orbit to drive motion."
        )]
        public bool disableSelfSpeed = false;

        [Header("Options")]
        [Tooltip(
            "If true and no Rigidbody2D is present, adds one (Kinematic) for smoother motion when needed."
        )]
        public bool autoAddPhysicsBody = true;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private bool _stopped;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;
            direction.Normalize();
            EnsurePhysics();

            // Apply immediate velocity so objects move on the same frame as spawn
            if (!disableSelfSpeed && _ctx != null && _ctx.PayloadRb2D != null)
            {
                var rb = _ctx.PayloadRb2D;
                if (rb.bodyType == RigidbodyType2D.Dynamic)
                    rb.linearVelocity = direction * Mathf.Max(0f, speed);
            }

            GameOverController.OnCountdownFinished += StopMovement;
        }

        private void EnsurePhysics()
        {
            if (!autoAddPhysicsBody)
                return;
            if (_ctx != null && _ctx.PayloadRb2D == null)
            {
                var rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                // Cache on context for downstream usage this frame
                if (_ctx != null)
                    _ctx.PayloadRb2D = rb;
            }
        }

        public void Tick(float dt)
        {
            if (_stopped || _ctx == null || _ctx.Payload == null)
                return;
            if (disableSelfSpeed)
                return;

            float spd = Mathf.Max(0f, speed);
            var rb = _ctx.PayloadRb2D;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity = direction * spd;
            }
            else
            {
                // No dynamic rigidbody: use transform translation so kinematic bodies still move
                _ctx.Payload.position += (Vector3)(direction * spd * dt);
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
    }
}
