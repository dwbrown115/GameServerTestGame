using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Rotates the payload around the target (or owner) at a given radius and angular speed.
    public class OrbitMechanic : MonoBehaviour, IMechanic
    {
        [Header("Orbit Settings")]
        [Tooltip("Angular speed in degrees per second")]
        public float angularSpeedDeg = 90f;

        [Tooltip("Orbit radius in world units")]
        public float radius = 2f;

        [Tooltip("Start angle in degrees")]
        public float startAngleDeg = 0f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _angle;
        private Rigidbody2D _rb;
        private bool _useRb;
        private bool _stopped;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            var centerInit = (_ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position);
            var payloadPos = (
                _ctx.Payload != null ? (Vector2)_ctx.Payload.position : (Vector2)transform.position
            );
            Vector2 dir = (payloadPos - (Vector2)centerInit);
            if (dir.sqrMagnitude > 0.0001f)
                _angle = Mathf.Atan2(dir.y, dir.x);
            else
                _angle = startAngleDeg * Mathf.Deg2Rad;
            _rb = _ctx != null ? _ctx.PayloadRb2D : null;
            _useRb = _rb != null;
            Vector3 pos = ComputeOrbitPosition();
            if (_useRb)
            {
                _rb.position = pos;
            }
            if (_ctx != null && _ctx.Payload != null)
                _ctx.Payload.position = pos;
            if (debugLogs)
                Debug.Log(
                    $"[OrbitMechanic] Initialize place. radius={radius} pos={(Vector2)pos}",
                    this
                );
            GameOverController.OnCountdownFinished += StopOrbit;
            // After initial placement, redistribute among siblings so spacing reflects total count
            if (transform.parent != null)
                Game.Procederal.Api.OrbitDistribution.Redistribute(transform.parent);
        }

        public void Tick(float dt)
        {
            if (_ctx == null || _ctx.Payload == null)
                return;
            if (_stopped)
                return;
            if (!_useRb)
            {
                _angle += angularSpeedDeg * Mathf.Deg2Rad * dt;
                var pos = ComputeOrbitPosition();
                _ctx.Payload.position = pos;
                if (debugLogs)
                {
                    var center = (_ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position);
                    float d = Vector2.Distance(center, pos);
                    Debug.Log(
                        $"[OrbitMechanic] Update place. center={(Vector2)center} angleRad={_angle:F2} radius={radius} dist={d:F2}",
                        this
                    );
                }
            }
        }

        private void FixedUpdate()
        {
            if (_stopped)
            {
                if (_rb != null)
                    _rb.linearVelocity = Vector2.zero;
                return;
            }
            if (_useRb && _ctx != null && _ctx.Payload != null)
            {
                _angle += angularSpeedDeg * Mathf.Deg2Rad * Time.fixedDeltaTime;
                var pos = ComputeOrbitPosition();
                _rb.MovePosition(pos);
                if (debugLogs)
                {
                    var center = (_ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position);
                    float d = Vector2.Distance(center, pos);
                    Debug.Log(
                        $"[OrbitMechanic] FixedUpdate place. center={(Vector2)center} angleRad={_angle:F2} radius={radius} dist={d:F2}",
                        this
                    );
                }
            }
        }

        private void OnDestroy()
        {
            GameOverController.OnCountdownFinished -= StopOrbit;
            // When one orbiting child disappears, re-space remaining siblings
            if (transform != null && transform.parent != null)
                Game.Procederal.Api.OrbitDistribution.Redistribute(transform.parent);
        }

        private void StopOrbit()
        {
            _stopped = true;
            if (_rb != null)
                _rb.linearVelocity = Vector2.zero;
        }

        private Vector3 ComputeOrbitPosition()
        {
            if (_ctx == null)
                return transform.position;
            var center = (_ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position);
            float r = Mathf.Max(0f, radius);
            var offset = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * r;
            return center + (Vector3)offset;
        }

        /// Allow external systems (like OrbitSpawnBehavior) to set the orbit angle.
        /// angleDeg: desired current angle in degrees (0deg at +X).
        /// repositionNow: when true, immediately move payload to the computed position.
        public void SetAngleDeg(float angleDeg, bool repositionNow = false)
        {
            _angle = angleDeg * Mathf.Deg2Rad;
            if (repositionNow)
            {
                if (_useRb && _rb != null)
                {
                    _rb.position = ComputeOrbitPosition();
                }
                else if (_ctx != null && _ctx.Payload != null)
                {
                    _ctx.Payload.position = ComputeOrbitPosition();
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform owner = _ctx != null ? _ctx.Owner : transform;
            Transform tgt = _ctx != null ? _ctx.Target : null;
            Vector3 center = (
                tgt != null ? tgt.position : (owner != null ? owner.position : transform.position)
            );
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, Mathf.Max(0f, radius));
        }
#endif
    }
}
