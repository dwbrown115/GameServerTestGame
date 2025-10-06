using UnityEngine;

namespace Mechanics.Corruption
{
    /// Applies a stacking damage-over-time debuff to targets on hit.
    /// Attach to payloads (Projectile/Aura/Beam/Strike root) and call TryApplyTo(target).
    [DisallowMultipleComponent]
    public class DamageOverTimeMechanic : MonoBehaviour, IMechanic, Mechanics.IPrimaryHitModifier
    {
        [Header("DoT Settings")]
        [Min(0)]
        public int damagePerTick = 1;

        [Min(0.01f)]
        public float interval = 0.5f;

        [Min(0f)]
        public float duration = 3f;

        [Tooltip(
            "When true, each application adds a new stack; when false, refreshes duration of existing effectId."
        )]
        public bool allowStacking = true;

        [Tooltip(
            "Identifier for the effect type (e.g., burn, poison). Used for stacking/refresh rules."
        )]
        public string effectId = "generic";

        [Header("Debug")]
        public bool debugLogs = true; // default on for troubleshooting

        private MechanicContext _ctx;
        private Mechanics.Corruption.DrainMechanic _drainOwner;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            // Cache owner's Drain if present to allow life-steal credit from DoT ticks
            _drainOwner =
                _ctx?.Owner != null
                    ? _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>()
                    : null;
        }

        public void Tick(float dt)
        {
            // Passive modifier: controller on the enemy handles ticking
        }

        /// Apply or stack a DoT debuff on the target's controller
        public bool TryApplyTo(Transform hit)
        {
            if (hit == null)
                return false;
            // Attach the controller to the object that actually has IDamageable
            // (not the absolute scene root), so the Debuffs parent appears on the mob.
            Transform attach = hit;
            var dmg = hit.GetComponentInParent<IDamageable>();
            if (dmg is Component comp)
            {
                attach = comp.transform;
            }
            if (debugLogs)
            {
                Debug.Log(
                    $"[DamageOverTimeMechanic] TryApplyTo hit='{hit.name}', attach='{attach.name}'",
                    this
                );
            }
            var controller = attach.GetComponent<Mob.DamageOverTimeController>();
            if (controller == null)
            {
                controller = attach.gameObject.AddComponent<Mob.DamageOverTimeController>();
                if (debugLogs)
                    Debug.Log(
                        $"[DamageOverTimeMechanic] Added DamageOverTimeController to '{attach.name}'",
                        this
                    );
            }
            if (controller == null)
                return false;

            var applied = controller.ApplyStack(
                damagePerTick: Mathf.Max(0, damagePerTick),
                tickInterval: Mathf.Max(0.01f, interval),
                duration: Mathf.Max(0f, duration),
                effectId: string.IsNullOrWhiteSpace(effectId) ? "generic" : effectId,
                allowStacking: allowStacking,
                drainOwner: _drainOwner
            );
            if (debugLogs && applied)
            {
                Debug.Log(
                    $"[DamageOverTimeMechanic] Applied DoT id='{effectId}' dmg/tick={damagePerTick} interval={interval}s duration={duration}s stacking={allowStacking} to {attach.name}",
                    this
                );
            }
            return applied;
        }

        // IPrimaryHitModifier: apply DoT to struck target
        public void OnPrimaryHit(in Mechanics.PrimaryHitInfo info)
        {
            TryApplyTo(info.target);
        }
    }
}
