using UnityEngine;

namespace Mechanics.Order
{
    /// Locks (stuns) an enemy's movement for a set amount of time on hit.
    /// Attach this as a modifier to payload mechanics (e.g., Projectiles, Aura, Beam)
    /// so they can apply stun to targets they affect.
    [DisallowMultipleComponent]
    public class LockMechanic : MonoBehaviour, IMechanic
    {
        [Tooltip("Seconds to stun the target on hit")]
        [Min(0f)]
        public float stunTime = 1f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            // Passive modifier; does nothing per-frame
        }

        /// Try apply stun to the given hit transform (collider or child of enemy)
        public bool TryApplyTo(Transform hit)
        {
            if (hit == null)
                return false;
            // Prefer parent root where AI likely resides
            var sc = hit.GetComponentInParent<StunController>();
            if (sc == null)
            {
                // Try on the same object if parent not found
                sc = hit.GetComponent<StunController>();
            }
            if (sc == null)
            {
                // As a last resort, add to the root of the collider
                var root = hit.root != null ? hit.root : hit;
                sc = root.gameObject.AddComponent<StunController>();
            }
            if (sc == null)
                return false;
            float dur = Mathf.Max(0f, stunTime);
            sc.ApplyStun(dur);
            if (debugLogs)
                Debug.Log($"[LockMechanic] Applied stun for {dur:0.###}s to {hit.name}", this);
            return true;
        }
    }
}
