using UnityEngine;

namespace Mechanics
{
    /// <summary>
    /// Generic contract for modifier components that want to react when ANY primary mechanic
    /// (Strike / Beam / Projectile / Aura / etc.) deals a discrete hit to a target.
    /// No reference to the concrete primary type is provided to preserve full decoupling.
    /// A lightweight PrimaryHitInfo struct conveys common, primary-agnostic data.
    /// Primaries construct a PrimaryHitInfo and invoke OnPrimaryHit on all co-located modifiers.
    /// </summary>
    public interface IPrimaryHitModifier
    {
        /// <summary>
        /// Invoked immediately after a primary confirms damage on a target.
        /// Implementations should be defensive (target may be null if primary had no transform context).
        /// </summary>
        void OnPrimaryHit(in PrimaryHitInfo info);
    }

    /// <summary>
    /// Common immutable data passed from primaries to hit modifiers.
    /// Additional fields can be appended without breaking existing implementors (struct is passed in).
    /// </summary>
    public readonly struct PrimaryHitInfo
    {
        public readonly Transform target;      // Transform that was damaged (or representative node)
        public readonly Vector2 hitPoint;      // World-space position of the hit/contact/effect origin
        public readonly Vector2 hitNormal;     // Optional normal (zero if not supplied)
        public readonly int damage;            // Damage dealt by this discrete hit (already applied)
        public readonly float time;            // Time.time when the hit occurred
        public readonly Object primarySource;  // (Optional) opaque reference to the primary component (for logging only)

        public PrimaryHitInfo(Transform target, Vector2 hitPoint, Vector2 hitNormal, int damage, Object primarySource)
        {
            this.target = target;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
            this.damage = damage;
            this.primarySource = primarySource;
            this.time = Time.time;
        }
    }
}
