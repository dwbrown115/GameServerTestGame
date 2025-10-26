using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Damages enemies within a radius around owner/target at fixed intervals.
    public class AuraMechanic : AreaDamageMechanicBase
    {
        [Header("Aura Settings")]
        [Tooltip("Center on target instead of owner if true and target exists")]
        public bool centerOnTarget = false;

        [Tooltip("Skip colliders tagged 'Player' or with PlayerHealth")]
        public bool excludePlayer = true;

        private Vector3 _lastVizPos;
        private float _lastVizDiameter;
        private float _vizLogTimer;

        public override Color spriteColor
        {
            get => base.spriteColor;
            set => base.spriteColor = value;
        }

        protected override Vector2 ResolveCenter()
        {
            if (Context != null && Context.Target != null && centerOnTarget)
                return Context.Target.position;
            return base.ResolveCenter();
        }

        protected override bool ShouldSkipCollider(Collider2D collider)
        {
            if (excludePlayer)
            {
                if (
                    collider.CompareTag("Player")
                    || collider.GetComponentInParent<PlayerHealth>() != null
                )
                    return true;
            }

            return base.ShouldSkipCollider(collider);
        }

        protected override void OnDamageApplied(
            IDamageable damageable,
            Collider2D sourceCollider,
            Vector2 center
        )
        {
            if (!debugLogs)
                return;
            Debug.Log(
                $"[AuraMechanic] Damaged {sourceCollider.name} -> {damageable.GetType().Name} for {damagePerInterval}",
                this
            );
        }

        protected override void OnTargetsDamaged(int totalDamage, Vector2 center)
        {
            base.OnTargetsDamaged(totalDamage, center);

            if (!debugLogs)
                return;
            Debug.Log(
                $"[AuraMechanic] Overlap center={center} radius={radius:F2} totalDamage={totalDamage}",
                this
            );
        }

        protected override void OnVisualizationUpdated(float dt, Vector3 center, float diameter)
        {
            _lastVizPos = center;
            _lastVizDiameter = diameter;

            if (!debugLogs)
                return;

            _vizLogTimer += dt;
            if (_vizLogTimer < 0.5f)
                return;

            _vizLogTimer = 0f;
            Debug.Log(
                $"[AuraMechanic] Viz pos={_lastVizPos} diameter={_lastVizDiameter:F2} radius={radius:F2}",
                this
            );
        }

        protected override string QueryTransformName => "AuraQuery";
        protected override string VisualizationRootName => "AuraViz";

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
