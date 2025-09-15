using UnityEngine;

namespace Mechanics.Purity
{
    /// A non-physical buff that heals the target (or owner) over time.
    /// Set MechanicHost.autoCreatePayload = false for pure buffs.
    public class RegenerationMechanic : MonoBehaviour, IMechanic
    {
        public enum TargetChoice
        {
            Owner,
            Target,
        }

        [Header("Regen Settings")]
        public TargetChoice applyTo = TargetChoice.Owner;

        [Tooltip("Hit points per second to restore")]
        public float healPerSecond = 2f;

        private MechanicContext _ctx;
        private PlayerHealth _playerHealth;
        private float _healAccumulator; // carry fractional healing across frames

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            var t =
                (applyTo == TargetChoice.Target && _ctx.Target != null) ? _ctx.Target : _ctx.Owner;
            if (t != null)
                _playerHealth = t.GetComponent<PlayerHealth>();
            _healAccumulator = 0f;
        }

        public void Tick(float dt)
        {
            if (_playerHealth == null || healPerSecond <= 0f)
                return;
            _healAccumulator += Mathf.Max(0f, healPerSecond) * Mathf.Max(0f, dt);
            int whole = Mathf.FloorToInt(_healAccumulator);
            if (whole > 0)
            {
                _playerHealth.Heal(whole);
                _healAccumulator -= whole;
            }
        }
    }
}
