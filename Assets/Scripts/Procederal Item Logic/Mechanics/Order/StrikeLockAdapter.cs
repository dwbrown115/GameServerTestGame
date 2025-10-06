using UnityEngine;

namespace Mechanics.Order
{
    /// <summary>
    /// Adapter that applies an attached LockMechanic when a strike hits a target.
    /// Requires a LockMechanic component on the same GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public class StrikeLockAdapter : MonoBehaviour, Mechanics.IStrikeHitModifier
    {
        private LockMechanic _lock;

        private void Awake()
        {
            _lock = GetComponent<LockMechanic>();
        }

        public void OnStrikeHit(
            Transform target,
            Vector2 hitPoint,
            int damage,
            Neuteral.StrikeMechanic source
        )
        {
            if (_lock == null || target == null)
                return;
            _lock.TryApplyTo(target);
        }
    }
}
