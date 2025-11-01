using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Standard circular orbit path defined by a radius and optional start angle.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrbitCircularPath : OrbitPathBehaviour
    {
        [Tooltip("Distance from the orbit center in world units.")]
        public float radius = 2f;

        [Tooltip("Fallback start angle in degrees when the payload is co-located with the center.")]
        public float startAngleDeg = 0f;

        public override float Size
        {
            get => Mathf.Max(0f, radius);
            set => radius = Mathf.Max(0f, value);
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
            Vector2 dir = currentPosition - center;
            if (dir.sqrMagnitude > 0.0001f)
                return Mathf.Atan2(dir.y, dir.x);

            return startAngleDeg * Mathf.Deg2Rad;
        }

        public override Vector3 EvaluatePosition(Vector2 center, float angleRad)
        {
            var offset =
                new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * Mathf.Max(0f, radius);
            return center + offset;
        }
    }
}
