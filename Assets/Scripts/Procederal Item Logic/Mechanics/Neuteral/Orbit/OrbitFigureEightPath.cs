using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Orbit path that follows a lemniscate (figure eight) described by (x^2 + y^2)^2 = a^2 (x^2 - y^2).
    /// Size controls the parameter a, effectively the horizontal lobe radius.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbitFigureEightPath : OrbitPathBehaviour
    {
        [Tooltip("Controls loop width; corresponds to 'a' in (x^2 + y^2)^2 = a^2 (x^2 - y^2).")]
        public float size = 2f;

        [Tooltip("Fallback start angle (degrees) when initial position cannot be inferred.")]
        public float startAngleDeg = 0f;

        public override float Size
        {
            get => Mathf.Max(0f, size);
            set => size = Mathf.Max(0f, value);
        }

        public override float StartAngleDeg
        {
            get => startAngleDeg;
            set => startAngleDeg = value;
        }

        public override float ResolveInitialAngle(
            Vector2 center,
            Vector2 currentPosition,
            float fallbackAngleRad
        )
        {
            Vector2 local = currentPosition - center;
            if (local.sqrMagnitude <= 0.0001f)
                return startAngleDeg * Mathf.Deg2Rad;

            float a = Mathf.Max(0.0001f, size);
            float x = local.x;
            float y = local.y;

            // Avoid division by zero; treat near-crossing as configured start angle.
            if (Mathf.Abs(x) <= 0.0001f)
                return startAngleDeg * Mathf.Deg2Rad;

            float sin = Mathf.Clamp(y / x, -1f, 1f);
            float sinSq = sin * sin;
            float cos = (x * (1f + sinSq)) / a;
            cos = Mathf.Clamp(cos, -1f, 1f);

            return Mathf.Atan2(sin, cos);
        }

        public override Vector3 EvaluatePosition(Vector2 center, float angleRad)
        {
            float a = Mathf.Max(0.0001f, size);
            float s = Mathf.Sin(angleRad);
            float c = Mathf.Cos(angleRad);
            float denom = 1f + s * s;
            if (denom <= 0.0001f)
                denom = 0.0001f;

            float x = a * c / denom;
            float y = a * s * c / denom;
            return center + new Vector2(x, y);
        }
    }
}
