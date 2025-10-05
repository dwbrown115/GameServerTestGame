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

        [Tooltip("Chance (0-1) to apply stun on a qualifying hit")]
        [Range(0f, 1f)]
        public float stunChance = 1f;

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
            if (debugLogs)
                Debug.Log(
                    $"[LockMechanic] Attempt stun on {hit.name} (stunTime={stunTime:0.###}, chance={stunChance:0.###})",
                    this
                );

            if (stunChance <= 0f)
            {
                if (debugLogs)
                    Debug.Log("[LockMechanic] Stun skipped (chance=0)", this);
                return false;
            }
            if (stunChance < 1f)
            {
                float roll = Random.value;
                if (roll > stunChance)
                {
                    if (debugLogs)
                        Debug.Log(
                            $"[LockMechanic] Roll {roll:0.###} > chance {stunChance:0.###}; no stun",
                            this
                        );
                    return false;
                }
                else if (debugLogs)
                {
                    Debug.Log(
                        $"[LockMechanic] Roll succeeded {roll:0.###} <= {stunChance:0.###}",
                        this
                    );
                }
            }
            // Search for any IStunnable in parents first
            IStunnable stunnable = hit.GetComponentInParent<IStunnable>();
            if (stunnable == null)
                stunnable = hit.GetComponent<IStunnable>();

            // If we only hit a parent container (e.g., MobSpawner) try to drill down to a mob child
            if (stunnable == null && hit.CompareTag("MobSpawner"))
            {
                foreach (Transform child in hit)
                {
                    stunnable = child.GetComponent<IStunnable>();
                    if (stunnable != null)
                    {
                        if (debugLogs)
                            Debug.Log(
                                $"[LockMechanic] Redirecting stun from spawner {hit.name} to child {child.name}",
                                this
                            );
                        break;
                    }
                }
            }

            if (stunnable == null)
            {
                // Add fallback StunController directly on the hit transform (preferred) then root
                var targetGo = hit.gameObject;
                var sc = targetGo.GetComponent<StunController>();
                if (sc == null)
                {
                    if (debugLogs)
                        Debug.Log($"[LockMechanic] Adding StunController to {targetGo.name}", this);
                    sc = targetGo.AddComponent<StunController>();
                }
                sc.hardFreeze = true; // ensure freeze mode
                stunnable = sc;
            }
            if (stunnable == null)
                return false;
            float dur = Mathf.Max(0f, stunTime);
            stunnable.ApplyStun(dur);
            if (debugLogs)
                Debug.Log($"[LockMechanic] Applied stun for {dur:0.###}s to {hit.name}", this);
            return true;
        }
    }
}
