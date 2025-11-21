using System.Collections.Generic;
using Game.Procederal;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Stationary or anchored area that repeatedly damages targets within a radius.
    /// Designed to pair with Drop/Throw movement behaviors for ground-based hazards.
    [DisallowMultipleComponent]
    public class DamageZoneMechanic : AreaDamageMechanicBase
    {
        private static readonly HashSet<DamageZoneMechanic> ActiveZones = new();

        [Header("Anchoring")]
        [Tooltip("Follow owner position instead of staying on the spawned payload.")]
        public bool followOwner = false;

        [Tooltip("Follow target position when available (overrides followOwner).")]
        public bool followTarget = false;

        [Tooltip("World-space offset applied after anchor selection.")]
        public Vector2 worldOffset = Vector2.zero;

        [Header("Lifetime")]
        [Tooltip("Delay before first damage tick is allowed (seconds).")]
        public float initialDelay = 0f;

        [Tooltip("Lifetime in seconds. Set 0 or negative for infinite.")]
        public float lifetimeSeconds = 4f;

        [Tooltip("Destroy the payload once lifetime elapses.")]
        public bool destroyOnExpire = true;

        [Tooltip(
            "Disable the mechanic component when lifetime elapses (ignored if destroyOnExpire is true)."
        )]
        public bool disableOnExpire = false;

        private float _elapsed;
        private float _delayRemaining;
        private bool _expired;
        private bool _detached;

        private void OnEnable()
        {
            ActiveZones.Add(this);
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        public override Color spriteColor
        {
            get => base.spriteColor;
            set => base.spriteColor = value;
        }

        public override void Initialize(MechanicContext ctx)
        {
            base.Initialize(ctx);

            _elapsed = 0f;
            _delayRemaining = Mathf.Max(0f, initialDelay);
            _expired = false;
            _detached = false;

            DetachFromSpawnParentIfStatic();

            if (_delayRemaining > 0f)
                ResetTickTimer();

            if (debugLogs)
            {
                Debug.Log(
                    $"[DamageZoneMechanic] Initialized radius={radius:F2} interval={interval:F2} lifetime={lifetimeSeconds:F2}",
                    this
                );
            }
        }

        protected override Vector2 ResolveCenter()
        {
            Vector2 center = transform.position;

            if (Context != null)
            {
                if (followTarget && Context.Target != null)
                    center = Context.Target.position;
                else if (followOwner && Context.Owner != null)
                    center = Context.Owner.position;
                else if (Context.Payload != null)
                    center = Context.Payload.position;
            }

            return center + worldOffset;
        }

        private void DetachFromSpawnParentIfStatic()
        {
            if (_detached)
                return;
            if (followOwner || followTarget)
                return;

            var parent = transform.parent;
            if (parent == null)
                return;

            Game.Procederal.ProcederalItemGenerator.DetachToWorld(
                gameObject,
                worldPositionStays: true
            );
            _detached = true;

            if (debugLogs)
            {
                Debug.Log(
                    "[DamageZoneMechanic] Detached from spawn parent to remain stationary.",
                    this
                );
            }
        }

        protected override bool PreTickUpdate(float dt, Vector2 center)
        {
            if (_expired)
                return false;

            _elapsed += dt;
            if (lifetimeSeconds > 0f && _elapsed >= lifetimeSeconds)
            {
                HandleExpire();
                return false;
            }

            if (_delayRemaining > 0f)
            {
                _delayRemaining -= dt;
                if (_delayRemaining > 0f)
                    return false;
                ResetTickTimer();
            }

            return true;
        }

        protected override string QueryTransformName => "DamageZoneQuery";
        protected override string VisualizationRootName => "DamageZoneViz";

        private void HandleExpire()
        {
            if (_expired)
                return;

            _expired = true;

            if (debugLogs)
                Debug.Log("[DamageZoneMechanic] Lifetime expired.", this);

            ForceDisableVisualization();

            if (QueryCollider != null)
                QueryCollider.enabled = false;

            if (destroyOnExpire)
            {
                DestroySelf();
                return;
            }

            if (Context != null && Context.PayloadRb2D != null)
                Context.PayloadRb2D.linearVelocity = Vector2.zero;

            if (disableOnExpire)
                enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        internal static bool HasZoneWithinRadius(Vector2 center, float radius)
        {
            float clampedRadius = Mathf.Max(0f, radius);
            float radiusSq = clampedRadius * clampedRadius;
            if (radiusSq <= 0f)
                return false;

            foreach (var zone in ActiveZones)
            {
                if (zone == null)
                    continue;
                var zoneTransform = zone.transform;
                if (zoneTransform == null)
                    continue;
                Vector2 zonePos = zoneTransform.position;
                if ((zonePos - center).sqrMagnitude <= radiusSq)
                    return true;
            }

            return false;
        }
    }
}
