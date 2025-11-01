using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Base class for defining orbit paths. Implementations compute positions from an angle parameter.
    /// </summary>
    public abstract class OrbitPathBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Gets or sets the primary size parameter for the path (e.g., radius for circular paths).
        /// Override in derived classes to expose path-specific sizing.
        /// </summary>
        public virtual float Size
        {
            get => 0f;
            set { }
        }

        /// <summary>
        /// Optional start angle (in degrees) respected by derived classes when no position data is available.
        /// </summary>
        public virtual float StartAngleDeg
        {
            get => 0f;
            set { }
        }

        /// <summary>
        /// Returns the initial angle (in radians) to use for this path.
        /// </summary>
        public abstract float ResolveInitialAngle(
            Vector2 center,
            Vector2 currentPosition,
            float fallbackAngleRad
        );

        /// <summary>
        /// Evaluates the orbit position for the supplied angle (radians) relative to the provided center.
        /// </summary>
        public abstract Vector3 EvaluatePosition(Vector2 center, float angleRad);
    }
}
