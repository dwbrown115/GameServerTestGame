using System;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Rotates the payload around the target (or owner) at a given angular speed using a pluggable path definition.
    public class OrbitMechanic : MonoBehaviour, IMechanic
    {
        [Header("Orbit Settings")]
        [Tooltip("Angular speed in degrees per second")]
        public float angularSpeedDeg = 90f;

        [
            Tooltip(
                "Optional fallback start angle in degrees when the path cannot resolve one from the payload position."
            ),
            SerializeField
        ]
        private float fallbackStartAngleDeg = 0f;

        [Header("Path")]
        [
            Tooltip(
                "Behaviour that determines the orbit path shape. Defaults to OrbitCircularPath if absent."
            ),
            SerializeField
        ]
        private OrbitPathBehaviour pathBehaviour;

        [Tooltip("Orbit path identifier (e.g., Circular, FigureEight)."), SerializeField]
        private string _pathId = "Circular";

        [Tooltip("Rotates the orbit path around the center in degrees."), SerializeField]
        private float pathRotationDeg = 0f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _angle;
        private Rigidbody2D _rb;
        private bool _useRb;
        private bool _stopped;

        public float radius
        {
            get => EnsurePath() != null ? Mathf.Max(0f, pathBehaviour.Size) : 0f;
            set
            {
                var path = EnsurePath();
                if (path != null)
                    path.Size = Mathf.Max(0f, value);
            }
        }

        public float startAngleDeg
        {
            get => EnsurePath() != null ? pathBehaviour.StartAngleDeg : fallbackStartAngleDeg;
            set
            {
                fallbackStartAngleDeg = value;
                var path = EnsurePath();
                if (path != null)
                    path.StartAngleDeg = value;
            }
        }

        public string pathId
        {
            get => _pathId;
            set
            {
                _pathId = value;
                pathBehaviour = null;
            }
        }

        public float PathRotationDeg
        {
            get => pathRotationDeg;
            set => pathRotationDeg = value;
        }

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            var path = EnsurePath();
            if (path == null)
            {
                Debug.LogWarning(
                    "[OrbitMechanic] Missing orbit path behaviour; unable to continue.",
                    this
                );
                return;
            }
            Vector2 centerInit = _ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position;
            Vector2 payloadPos =
                _ctx.Payload != null ? (Vector2)_ctx.Payload.position : (Vector2)transform.position;
            float orientationRad = GetOrientationRad();
            Vector2 resolvePos = Mathf.Approximately(orientationRad, 0f)
                ? payloadPos
                : RotateAround2D(payloadPos, centerInit, -orientationRad);
            _angle = path.ResolveInitialAngle(
                centerInit,
                resolvePos,
                fallbackStartAngleDeg * Mathf.Deg2Rad
            );
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
            Vector2 center = _ctx.Target != null ? _ctx.Target.position : _ctx.Owner.position;
            var path = EnsurePath();
            if (path == null)
                return center;
            Vector3 basePos = path.EvaluatePosition(center, _angle);
            float orientationRad = GetOrientationRad();
            if (Mathf.Approximately(orientationRad, 0f))
                return basePos;
            Vector2 rotated = RotateAround2D(basePos, center, orientationRad);
            return new Vector3(rotated.x, rotated.y, basePos.z);
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
            if (!Application.isPlaying)
                return;

            Transform owner = _ctx != null ? _ctx.Owner : transform;
            Transform tgt = _ctx != null ? _ctx.Target : null;
            Vector3 center =
                tgt != null ? tgt.position : (owner != null ? owner.position : transform.position);
            var path = pathBehaviour != null ? pathBehaviour : GetComponent<OrbitPathBehaviour>();
            if (path == null)
                return;

            float orientationRad = GetOrientationRad();

            if (path is OrbitCircularPath circular)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(center, Mathf.Max(0f, circular.radius));
                return;
            }

            const int sampleCount = 96;
            Vector3? previous = null;
            Gizmos.color = Color.cyan;
            for (int i = 0; i <= sampleCount; i++)
            {
                float t = (Mathf.PI * 2f) * i / sampleCount;
                Vector3 pos = path.EvaluatePosition(center, t);
                if (!Mathf.Approximately(orientationRad, 0f))
                {
                    Vector2 rotated = RotateAround2D(pos, center, orientationRad);
                    pos = new Vector3(rotated.x, rotated.y, pos.z);
                }

                if (previous.HasValue)
                    Gizmos.DrawLine(previous.Value, pos);

                previous = pos;
            }
        }
#endif

        private OrbitPathBehaviour EnsurePath()
        {
            string normalized = NormalizePathId(_pathId);
            Type desiredType = ResolvePathType(normalized);

            if (pathBehaviour != null && pathBehaviour.GetType() != desiredType)
            {
                DestroyPathBehaviour(pathBehaviour);
                pathBehaviour = null;
            }

            if (pathBehaviour == null)
            {
                var existing = GetComponent(desiredType) as OrbitPathBehaviour;
                if (existing != null)
                {
                    pathBehaviour = existing;
                }
                else
                {
                    var created = gameObject.AddComponent(desiredType) as OrbitPathBehaviour;
                    pathBehaviour = created;
                }
            }

            if (pathBehaviour != null)
                pathBehaviour.StartAngleDeg = fallbackStartAngleDeg;

            return pathBehaviour;
        }

        private static void DestroyPathBehaviour(OrbitPathBehaviour behaviour)
        {
            if (behaviour == null)
                return;
            if (Application.isPlaying)
                Destroy(behaviour);
            else
                DestroyImmediate(behaviour);
        }

        private static string NormalizePathId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "circular";
            string trimmed = raw.Trim();
            string collapsed = trimmed
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty);
            string lower = collapsed.ToLowerInvariant();
            string withoutKeywords = lower
                .Replace("orbit", string.Empty)
                .Replace("path", string.Empty);
            return withoutKeywords;
        }

        private static Type ResolvePathType(string normalized)
        {
            switch (normalized)
            {
                case "figure8":
                case "figureeight":
                case "lemniscate":
                case "orbitfigureeight":
                case "orbitfigureeightpath":
                    return typeof(OrbitFigureEightPath);
                default:
                    return typeof(OrbitCircularPath);
            }
        }

        private float GetOrientationRad()
        {
            return pathRotationDeg * Mathf.Deg2Rad;
        }

        private static Vector2 RotateAround2D(Vector2 point, Vector2 pivot, float angleRad)
        {
            float sin = Mathf.Sin(angleRad);
            float cos = Mathf.Cos(angleRad);
            Vector2 translated = point - pivot;
            return pivot
                + new Vector2(
                    translated.x * cos - translated.y * sin,
                    translated.x * sin + translated.y * cos
                );
        }

        private static Vector2 RotateAround2D(Vector3 point, Vector2 pivot, float angleRad)
        {
            return RotateAround2D((Vector2)point, pivot, angleRad);
        }
    }
}
