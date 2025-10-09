using UnityEngine;

namespace Mechanics.Order
{
    /// Tracks the nearest enemy (tagged 'Mob') and steers the payload toward it.
    /// Works best with ProjectileMechanic; respects disableSelfSpeed and Orbit (does nothing if self-movement disabled).
    [DisallowMultipleComponent]
    public class TrackMechanic : MonoBehaviour, IMechanic
    {
        [Header("Tracking")]
        [Tooltip("How quickly to rotate the direction toward the target (deg/sec)")]
        [Min(0f)]
        public float turnRateDegPerSec = 360f;

        [Tooltip("Max distance to search for a target; 0 means unlimited")]
        [Min(0f)]
        public float searchRadius = 0f;

        [Tooltip("How often to re-acquire a target (seconds)")]
        [Min(0f)]
        public float retargetInterval = 0.1f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private Transform _currentTarget;
        private float _retargetTimer;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _retargetTimer = 0f;
        }

        public void Tick(float dt)
        {
            if (_ctx == null)
                return;
            // If payload movement is handled by Orbit or disabled, don't steer
            var proj = GetComponent<Mechanics.Neuteral.ProjectileMechanic>();
            if (proj != null && proj.disableSelfSpeed)
                return;

            // Acquire/refresh current target
            _retargetTimer += dt;
            if (_currentTarget == null || _retargetTimer >= Mathf.Max(0f, retargetInterval))
            {
                _currentTarget = FindNearestMob();
                _retargetTimer = 0f;
            }
            if (_currentTarget == null)
                return;

            Vector2 to = (Vector2)(_currentTarget.position - _ctx.Payload.position);
            if (to.sqrMagnitude < 1e-6f)
                return;

            Vector2 desired = to.normalized;

            if (proj != null)
            {
                // Steer projectile direction smoothly toward the target
                Vector2 cur =
                    proj.direction.sqrMagnitude > 1e-6f ? proj.direction.normalized : Vector2.right;
                float maxRotate = Mathf.Max(0f, turnRateDegPerSec) * dt;
                Vector2 newDir = RotateToward(cur, desired, maxRotate);
                proj.direction = newDir;
                // Optional: if using Rigidbody2D velocity, ProjectileMechanic will apply it this frame
            }
            else if (_ctx.PayloadRb2D != null)
            {
                // If no ProjectileMechanic, gently steer the body velocity
                Vector2 curVel = _ctx.PayloadRb2D.linearVelocity;
                float speed = curVel.magnitude;
                if (speed > 0.01f)
                {
                    Vector2 cur = curVel / speed;
                    float maxRotate = Mathf.Max(0f, turnRateDegPerSec) * dt;
                    Vector2 newDir = RotateToward(cur, desired, maxRotate);
                    _ctx.PayloadRb2D.linearVelocity = newDir * speed;
                }
                else
                {
                    // If stationary, nudge toward target
                    _ctx.PayloadRb2D.linearVelocity = desired * speed;
                }
            }
        }

        private Transform FindNearestMob()
        {
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
                return null;
            Transform center = _ctx.Payload != null ? _ctx.Payload : transform;
            float bestDist2 = float.MaxValue;
            Transform best = null;
            foreach (var go in mobs)
            {
                if (go == null)
                    continue;
                float d2 = (go.transform.position - center.position).sqrMagnitude;
                if (searchRadius > 0f && d2 > searchRadius * searchRadius)
                    continue;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = go.transform;
                }
            }
            if (debugLogs && best != null)
                Debug.Log(
                    $"[TrackMechanic] Target={best.name} dist={Mathf.Sqrt(bestDist2):0.##}",
                    this
                );
            return best;
        }

        private static Vector2 RotateToward(Vector2 from, Vector2 to, float maxDeg)
        {
            float curAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(curAngle, targetAngle);
            float step = Mathf.Clamp(delta, -maxDeg, maxDeg);
            float newAngle = curAngle + step;
            float rad = newAngle * Mathf.Rad2Deg; // Oops; fix to Deg2Rad below
            // Correct conversion
            rad = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        }
    }
}
