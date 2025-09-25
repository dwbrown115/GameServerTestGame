using UnityEngine;

namespace Mechanics.Chaos
{
    /// Modifier: On mob hit, either bounce in a new random direction or get destroyed.
    /// Each bounce increases the destroy chance. If idle (no interactions) for a duration, auto-destroy.
    [DisallowMultipleComponent]
    public class BounceMechanic : MonoBehaviour, IMechanic
    {
        [Header("Bounce Settings")]
        [Tooltip("Initial chance (0..1) that the payload is destroyed instead of bouncing on hit.")]
        [Range(0f, 1f)]
        public float baseDestroyChance = 0.2f;

        [Tooltip("Amount added to destroy chance after each successful bounce.")]
        [Range(0f, 1f)]
        public float destroyChanceIncreasePerBounce = 0.1f;

        [Tooltip("Seconds of no interactions before auto-destroy.")]
        public float idleLifetime = 2f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private int _bounces;
        private float _idleTimer;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _bounces = 0;
            _idleTimer = 0f;
        }

        public void Tick(float dt)
        {
            _idleTimer += dt;
            if (idleLifetime > 0f && _idleTimer >= idleLifetime)
            {
                if (debugLogs)
                    Debug.Log("[Bounce] Auto-destroy by idle lifetime", this);
                if (_ctx?.Payload != null)
                    Destroy(_ctx.Payload.gameObject);
                else
                    Destroy(gameObject);
            }
        }

        /// Call when a mob hit occurs. Decides whether to destroy or bounce, and outputs new direction.
        public bool TryHandleHit(out bool shouldDestroy, out Vector2 newDirection)
        {
            _idleTimer = 0f; // reset idle on interaction
            float chance = Mathf.Clamp01(
                baseDestroyChance + destroyChanceIncreasePerBounce * _bounces
            );
            float roll = Random.value;
            if (debugLogs)
                Debug.Log(
                    $"[Bounce] hit: bounces={_bounces} chance={chance:F2} roll={roll:F2}",
                    this
                );
            if (roll < chance)
            {
                shouldDestroy = true;
                newDirection = Vector2.zero;
                return true;
            }
            // Bounce: pick a new random unit direction
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 1e-4f)
                dir = Vector2.right;
            newDirection = dir.normalized;
            _bounces++;
            shouldDestroy = false;
            return true;
        }
    }
}
