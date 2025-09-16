using UnityEngine;

namespace Mechanics.Corruption
{
    /// Event-based drain: listens for reported damage and heals the owner by lifeStealRatio.
    /// No colliders or periodic ticks; other mechanics must call ReportDamage.
    public class DrainMechanic : MonoBehaviour, IMechanic
    {
        [Header("Drain Settings")]
        [Range(0f, 1f)]
        public float lifeStealRatio = 0.5f; // 50% of damage dealt

        [Tooltip("Probability [0..1] that a life steal will occur on a reported damage event.")]
        [Range(0f, 1f)]
        public float lifeStealChance = 1f; // 100% by default

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private PlayerHealth _ownerHealth;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            if (_ctx?.Owner != null)
                _ownerHealth = _ctx.Owner.GetComponent<PlayerHealth>();
            if (_ownerHealth == null)
            {
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                    _ownerHealth = playerGo.GetComponent<PlayerHealth>();
            }
        }

        // No periodic work needed
        public void Tick(float dt) { }

        /// Report damage dealt by a primary mechanic; converts to healing immediately.
        public void ReportDamage(int totalDamage)
        {
            if (_ownerHealth == null || totalDamage <= 0 || lifeStealRatio <= 0f)
                return;
            // Chance gate
            if (lifeStealChance < 1f)
            {
                if (lifeStealChance <= 0f)
                    return;
                if (Random.value > lifeStealChance)
                    return;
            }
            int heal = Mathf.RoundToInt(totalDamage * lifeStealRatio);
            if (heal > 0)
            {
                _ownerHealth.Heal(heal);
                if (debugLogs)
                    Debug.Log(
                        $"[DrainMechanic] Healed owner {heal} from damage {totalDamage} (ratio={lifeStealRatio:F2}, chance={lifeStealChance:P0})",
                        this
                    );
            }
        }
    }
}
